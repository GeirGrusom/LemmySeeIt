using Avalonia.Media.Imaging;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

[TestFixture]
internal sealed class ImageViewerTests
{
    private TestServices services = null!;

    [SetUp]
    public void CreateServices() => services = new TestServices();

    private static PostSummary ImagePost(int id = 10) => Sample.ImagePostSummary(id);

    /// <summary>A gallery whose contents the test controls directly.</summary>
    private sealed class StubGallery : IImageGallery
    {
        internal StubGallery(params PostSummary[] images) => Images = [.. images];

        public System.Collections.Immutable.ImmutableArray<PostSummary> Images { get; set; }

        public bool CanLoadMore => false;

        public Task LoadMoreAsync() => Task.CompletedTask;
    }

    private MainViewModel CreateShell()
    {
        services.FeedReturns(Sample.PostPage(3, null));
        return new MainViewModel(services.Services, AppSettings.Default);
    }

    [Test]
    public async Task ShowImage_OpensTheViewerOverTheFeedWithoutNavigatingAwayFromIt()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        PageViewModel? feed = shell.CurrentPage;

        shell.ShowImage(ImagePost(), new StubGallery(ImagePost()));

        Assert.Multiple(() =>
        {
            Assert.That(shell.ImageViewer, Is.Not.Null);
            Assert.That(shell.CurrentPage, Is.SameAs(feed), "the feed is still underneath");
            Assert.That(shell.CanPop, Is.False, "the viewer is an overlay, not a page");
        });
    }

    [Test]
    public async Task ShowImage_IgnoresAPostThatIsNotAPicture()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        shell.ShowImage(Sample.PostSummary(), new StubGallery());

        Assert.That(shell.ImageViewer, Is.Null);
    }

    /// <summary>Back dismisses the picture before it touches navigation.</summary>
    [Test]
    public async Task BackClosesTheViewerBeforeUnwindingThePageStack()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.Push(new SearchViewModel(services.Services, shell, services.Api, AppSettings.Default));
        shell.ShowImage(ImagePost(), new StubGallery(ImagePost()));

        bool handled = shell.TryGoBack();

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(shell.ImageViewer, Is.Null);
            Assert.That(shell.CanPop, Is.True, "the page underneath is untouched");
        });
    }

    [Test]
    public async Task ClosingTheViewerReleasesIt()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.ShowImage(ImagePost(), new StubGallery(ImagePost()));
        ImageViewerViewModel viewer = shell.ImageViewer!;

        viewer.CloseCommand.Execute(null);

        Assert.That(shell.ImageViewer, Is.Null);
    }

    [Test]
    public async Task OpeningASecondPictureReplacesTheFirst()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.ShowImage(ImagePost(10), new StubGallery(ImagePost(10), ImagePost(11)));
        ImageViewerViewModel first = shell.ImageViewer!;

        shell.ShowImage(ImagePost(11), new StubGallery(ImagePost(10), ImagePost(11)));

        Assert.Multiple(() =>
        {
            Assert.That(shell.ImageViewer, Is.Not.SameAs(first));
            Assert.That(shell.ImageViewer!.Summary!.Id, Is.EqualTo(new PostId(11)));
        });
    }

    [Test]
    public async Task SwitchingInstanceClosesAnOpenPicture()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.ShowImage(ImagePost(), new StubGallery(ImagePost()));
        shell.InstanceInput = "sh.itjust.works";

        await shell.ApplyInstanceCommand.ExecuteAsync(null);

        Assert.That(shell.ImageViewer, Is.Null);
    }

    [Test]
    public async Task LoadAsync_AsksForTheFullSizeImageOutsideTheSharedCache()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        shell.ShowImage(ImagePost(), new StubGallery(ImagePost()));
        await shell.ImageViewer!.LoadAsync();

        Assert.Multiple(async () =>
        {
            await services.ImageLoader.Received().LoadPictureAsync(
                Arg.Is<WebLink>(link => link.Value == "https://example.com/art-10.png"),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());
            await services.ImageLoader.DidNotReceive().LoadAsync(
                Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task AnImageThatCannotBeLoadedSaysSoRatherThanHangingOnASpinner()
    {
        services.ImageLoader.LoadPictureAsync(Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AnimatedImage?>(null));
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        shell.ShowImage(ImagePost(), new StubGallery(ImagePost()));
        await shell.ImageViewer!.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(shell.ImageViewer!.HasFailed, Is.True);
            Assert.That(shell.ImageViewer!.IsLoading, Is.False);
        });
    }

    /// <summary>
    /// AVIF originals cannot be decoded by the Skia build Avalonia ships, but pict-rs thumbnails
    /// are always jpeg, png or webp. A softer picture beats an error screen.
    /// </summary>
    [Test]
    public async Task AnUndecodableOriginalFallsBackToTheServersPreview()
    {
        PostSummary post = Sample.PostSummary() with
        {
            Post = Sample.Post(url: "https://example.com/art.avif", contentType: "image/avif") with
            {
                Thumbnail = WebLink.Parse("https://example.com/art-thumb.jpeg"),
            },
        };

        services.ImageLoader
            .LoadPictureAsync(Arg.Is<WebLink>(link => link.Value.EndsWith(".avif", StringComparison.Ordinal)),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AnimatedImage?>(null));

        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.ShowImage(post, new StubGallery(post));
        await shell.ImageViewer!.LoadAsync();

        // Whether a picture actually appears is decided by the decoder, which needs a real platform;
        // ImageCodecTests covers that end to end. What matters here is that the fallback is asked for.
        await services.ImageLoader.Received().LoadPictureAsync(
            Arg.Is<WebLink>(link => link.Value.EndsWith("-thumb.jpeg", StringComparison.Ordinal)),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task WhenNeitherTheOriginalNorThePreviewDecodesTheViewerSaysSo()
    {
        services.ImageLoader.LoadPictureAsync(Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AnimatedImage?>(null));

        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.ShowImage(ImagePost(), new StubGallery(ImagePost()));
        await shell.ImageViewer!.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(shell.ImageViewer!.HasFailed, Is.True);
            Assert.That(shell.ImageViewer!.IsShowingReducedQuality, Is.False);
        });
    }

    [Test]
    public void TheViewerStartsFittedToTheScreen()
    {
        var viewer = new ImageViewerViewModel(
            new StubGallery(ImagePost()), ImagePost(), services.ImageLoader, () => { });

        Assert.That(viewer.Zoom, Is.EqualTo(ZoomState.Fitted));
        viewer.Dispose();
    }

    [Test]
    public void ResetZoomReturnsToTheWholePicture()
    {
        var viewer = new ImageViewerViewModel(
            new StubGallery(ImagePost()), ImagePost(), services.ImageLoader, () => { });
        viewer.Apply(new ZoomState(3, new Avalonia.Vector(20, 20)));

        viewer.ResetZoomCommand.Execute(null);

        Assert.That(viewer.Zoom, Is.EqualTo(ZoomState.Fitted));
        viewer.Dispose();
    }

    [Test]
    public async Task AFeedRowOffersTheImageOnlyWhenThePostIsOne()
    {
        var navigator = new RecordingNavigator();
        services.FeedReturns(new PostPage([ImagePost(10), Sample.PostSummary(11)], null));
        using var feed = new FeedViewModel(services.Services, navigator, services.Api, AppSettings.Default);
        await feed.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(feed.Posts[0].CanViewImage, Is.True);
            Assert.That(feed.Posts[1].CanViewImage, Is.False);
            Assert.That(feed.Posts[1].ViewImageCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task TappingTheThumbnailShowsThePictureRatherThanOpeningThePost()
    {
        var navigator = new RecordingNavigator();
        services.FeedReturns(new PostPage([ImagePost()], null));
        using var feed = new FeedViewModel(services.Services, navigator, services.Api, AppSettings.Default);
        await feed.LoadAsync();

        feed.Posts[0].ViewImageCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(navigator.ImagesShown, Has.Count.EqualTo(1));
            Assert.That(navigator.Pushed, Is.Empty, "the post was not opened");
        });
    }
}
