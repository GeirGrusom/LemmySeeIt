using System.Collections.Immutable;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// Moving between the pictures on a page without going back to it — the thing that makes an art or
/// comic community browsable.
/// </summary>
[TestFixture]
internal sealed class ImageGalleryTests
{
    private TestServices services = null!;

    [SetUp]
    public void CreateServices() => services = new TestServices();

    private sealed class StubGallery : IImageGallery
    {
        internal StubGallery(params PostSummary[] images) => Images = [.. images];

        public ImmutableArray<PostSummary> Images { get; set; }

        public bool CanLoadMore => false;

        public Task LoadMoreAsync() => Task.CompletedTask;
    }

    private ImageViewerViewModel Viewer(IImageGallery gallery, PostSummary start) =>
        new(gallery, start, services.ImageLoader, () => { });

    private static PostSummary[] Pictures(int count) =>
        [.. Enumerable.Range(0, count).Select(index => Sample.ImagePostSummary(10 + index, $"Picture {index}"))];

    [Test]
    public async Task TheViewerKnowsWhereItSitsInThePage()
    {
        PostSummary[] pictures = Pictures(4);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[1]);

        await viewer.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewer.PositionLabel, Is.EqualTo("2 / 4"));
            Assert.That(viewer.CanShowPrevious, Is.True);
            Assert.That(viewer.CanShowNext, Is.True);
        });
    }

    [Test]
    public async Task ASinglePictureShowsNoCounterAndNowhereToGo()
    {
        PostSummary[] pictures = Pictures(1);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[0]);

        await viewer.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewer.PositionLabel, Is.Empty);
            Assert.That(viewer.CanShowPrevious, Is.False);
            Assert.That(viewer.CanShowNext, Is.False);
        });
    }

    [Test]
    public async Task MovingOnShowsTheNextPicture()
    {
        PostSummary[] pictures = Pictures(3);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[0]);
        await viewer.LoadAsync();

        await viewer.ShowNextAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewer.Summary!.Id, Is.EqualTo(pictures[1].Id));
            Assert.That(viewer.Title, Is.EqualTo("Picture 1"));
            Assert.That(viewer.PositionLabel, Is.EqualTo("2 / 3"));
        });
    }

    [Test]
    public async Task MovingBackShowsThePreviousPicture()
    {
        PostSummary[] pictures = Pictures(3);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[2]);
        await viewer.LoadAsync();

        await viewer.ShowPreviousAsync();

        Assert.That(viewer.Summary!.Id, Is.EqualTo(pictures[1].Id));
    }

    [Test]
    public async Task TheEndsOfThePageAreTheEnds()
    {
        PostSummary[] pictures = Pictures(2);
        using ImageViewerViewModel first = Viewer(new StubGallery(pictures), pictures[0]);
        using ImageViewerViewModel last = Viewer(new StubGallery(pictures), pictures[1]);
        await first.LoadAsync();
        await last.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(first.CanShowPrevious, Is.False);
            Assert.That(first.ShowPreviousCommand.CanExecute(null), Is.False);
            Assert.That(last.CanShowNext, Is.False);
            Assert.That(last.ShowNextCommand.CanExecute(null), Is.False);
        });
    }

    /// <summary>
    /// The next picture starts fitted. Carrying the previous zoom over would drop the reader into a
    /// random corner of an image they have not seen yet.
    /// </summary>
    [Test]
    public async Task MovingOnResetsTheZoom()
    {
        PostSummary[] pictures = Pictures(2);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[0]);
        await viewer.LoadAsync();
        viewer.Apply(new ZoomState(4, new Avalonia.Vector(30, 30)));

        await viewer.ShowNextAsync();

        Assert.That(viewer.Zoom, Is.EqualTo(ZoomState.Fitted));
    }

    [Test]
    public async Task EachPictureIsFetchedAtFullSizeAsItIsReached()
    {
        PostSummary[] pictures = Pictures(2);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[0]);
        await viewer.LoadAsync();

        await viewer.ShowNextAsync();

        await services.ImageLoader.Received(1).LoadPictureAsync(
            Arg.Is<WebLink>(link => link.Value.EndsWith("art-11.png", StringComparison.Ordinal)),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The page keeps growing as it is scrolled. Freezing the list when the viewer opened would
    /// strand the reader at whatever happened to be loaded when they tapped.
    /// </summary>
    [Test]
    public async Task PicturesTheFeedLoadsLaterBecomeReachable()
    {
        PostSummary[] pictures = Pictures(2);
        var gallery = new StubGallery(pictures);
        using ImageViewerViewModel viewer = Viewer(gallery, pictures[1]);
        await viewer.LoadAsync();

        Assert.That(viewer.CanShowNext, Is.False, "nothing after it yet");

        gallery.Images = [.. Pictures(4)];
        await viewer.ShowPreviousAsync();
        await viewer.ShowNextAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewer.CanShowNext, Is.True);
            Assert.That(viewer.PositionLabel, Is.EqualTo("2 / 4"));
        });
    }

    /// <summary>A feed's gallery is its pictures only — articles with scraped previews are not pictures.</summary>
    [Test]
    public async Task AFeedOffersOnlyItsPicturesAsAGallery()
    {
        services.FeedReturns(Sample.MixedPage(imageCount: 3, articleCount: 2));
        using var feed = new FeedViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await feed.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(feed.Posts, Has.Count.EqualTo(5), "the feed itself shows everything");
            Assert.That(feed.Images, Has.Length.EqualTo(3), "the gallery is pictures only");
            Assert.That(feed.Images.Select(image => image.Post.IsImage), Is.All.True);
        });
    }

    [Test]
    public async Task TappingAThumbnailHandsTheWholeFeedToTheViewer()
    {
        var navigator = new RecordingNavigator();
        services.FeedReturns(Sample.MixedPage(imageCount: 3, articleCount: 1));
        using var feed = new FeedViewModel(services.Services, navigator, services.Api, AppSettings.Default);
        await feed.LoadAsync();

        feed.Posts.First(card => card.CanViewImage).ViewImageCommand.Execute(null);

        Assert.That(navigator.LastGallery, Is.SameAs(feed));
    }

    [Test]
    public async Task ASearchResultPageIsAGalleryToo()
    {
        services.Api.SearchAsync(Arg.Any<Lemmy.Api.SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SearchResults(
                [Sample.ImagePostSummary(10), Sample.PostSummary(11), Sample.ImagePostSummary(12)], [], [])));
        using var search = new SearchViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        search.QueryText = "art";

        await search.SearchAsync();

        Assert.That(search.Images, Has.Length.EqualTo(2));
    }

    /// <summary>
    /// Flicking faster than the network must not let a slow earlier load land on a later picture.
    /// </summary>
    [Test]
    public async Task ASlowLoadDoesNotOverwriteAPictureTheReaderHasAlreadyMovedPast()
    {
        // The first picture never finishes downloading; the second answers at once.
        var slow = new TaskCompletionSource<PictureLoad>();
        bool isFirstCall = true;
        services.ImageLoader.LoadPictureAsync(Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (!isFirstCall)
                {
                    return Task.FromResult(PictureLoad.Unreachable);
                }

                isFirstCall = false;
                return slow.Task;
            });

        PostSummary[] pictures = Pictures(2);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[0]);

        Task first = viewer.LoadAsync();
        await viewer.ShowNextAsync();

        // The abandoned first load now lands, well after the reader moved on.
        slow.SetResult(PictureLoad.Unreachable);
        await first;

        Assert.Multiple(() =>
        {
            Assert.That(viewer.Summary!.Id, Is.EqualTo(pictures[1].Id));
            Assert.That(viewer.IsLoading, Is.False, "the stale load must not reinstate a spinner");
        });
    }
}
