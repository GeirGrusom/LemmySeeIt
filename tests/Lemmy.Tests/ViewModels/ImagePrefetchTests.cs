using System.Collections.Immutable;
using Avalonia;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// Fetching the next picture while the reader looks at this one. Bitmaps need the platform, so
/// these run on Avalonia's headless host.
/// </summary>
[TestFixture]
internal sealed class ImagePrefetchTests
{
    private TestServices services = null!;

    [SetUp]
    public void CreateServices()
    {
        services = new TestServices();
        services.ImageLoader
            .LoadPictureAsync(Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(PictureLoad.Loaded(Picture())));
    }

    /// <summary>A one-frame picture of a given size, so budgets can be exercised.</summary>
    private static AnimatedImage Picture(int side = 8) =>
        new([
            new AnimationFrame(
                new WriteableBitmap(new PixelSize(side, side), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul),
                TimeSpan.Zero),
        ]);

    private sealed class StubGallery(params PostSummary[] images) : IImageGallery
    {
        public ImmutableArray<PostSummary> Images { get; } = [.. images];

        public bool CanLoadMore => false;

        public Task LoadMoreAsync() => Task.CompletedTask;
    }

    private static PostSummary[] Pictures(int count) =>
        [.. Enumerable.Range(0, count).Select(index => Sample.ImagePostSummary(10 + index, $"Picture {index}"))];

    private ImageViewerViewModel Viewer(IImageGallery gallery, PostSummary start) =>
        new(gallery, start, services.ImageLoader, () => { });

    private Task Requested(PostSummary post) =>
        services.ImageLoader.Received().LoadPictureAsync(
            Arg.Is<WebLink>(link => link.Value == post.Post.FullImage!.Value.Value),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());

    [AvaloniaTest]
    public async Task ShowingAPictureFetchesTheNextOneAhead()
    {
        PostSummary[] pictures = Pictures(3);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[0]);

        await viewer.LoadAsync();

        await Requested(pictures[1]);
    }

    /// <summary>The whole point: arriving at a prefetched picture must not show a spinner.</summary>
    [AvaloniaTest]
    public async Task MovingToAPrefetchedPictureShowsItWithoutLoading()
    {
        PostSummary[] pictures = Pictures(3);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[0]);
        await viewer.LoadAsync();

        await viewer.ShowNextAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewer.IsLoading, Is.False);
            Assert.That(viewer.Image, Is.Not.Null);
            Assert.That(viewer.Summary!.Id, Is.EqualTo(pictures[1].Id));
        });
    }

    [AvaloniaTest]
    public async Task APrefetchedPictureIsNotFetchedTwice()
    {
        PostSummary[] pictures = Pictures(3);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[0]);
        await viewer.LoadAsync();

        await viewer.ShowNextAsync();

        await services.ImageLoader.Received(1).LoadPictureAsync(
            Arg.Is<WebLink>(link => link.Value == pictures[1].Post.FullImage!.Value.Value),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [AvaloniaTest]
    public async Task ArrivingSomewhereFetchesTheNextOneFromThere()
    {
        PostSummary[] pictures = Pictures(4);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[0]);
        await viewer.LoadAsync();

        await viewer.ShowNextAsync();

        await Requested(pictures[2]);
    }

    /// <summary>Turning round makes the picture behind the one worth having ready.</summary>
    [AvaloniaTest]
    public async Task GoingBackwardsFetchesBackwards()
    {
        PostSummary[] pictures = Pictures(4);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[2]);
        await viewer.LoadAsync();

        await viewer.ShowPreviousAsync();

        await Requested(pictures[0]);
    }

    [AvaloniaTest]
    public async Task TheLastPictureHasNothingToFetchAhead()
    {
        PostSummary[] pictures = Pictures(2);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[1]);

        await viewer.LoadAsync();

        await services.ImageLoader.Received(1).LoadPictureAsync(
            Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [AvaloniaTest]
    public async Task ASinglePictureFetchesNothingElse()
    {
        PostSummary[] pictures = Pictures(1);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[0]);

        await viewer.LoadAsync();

        await services.ImageLoader.Received(1).LoadPictureAsync(
            Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Two large pictures held at once is how a phone kills the app mid-scroll, so an oversized
    /// neighbour is fetched, measured and dropped rather than kept.
    /// </summary>
    [AvaloniaTest]
    public async Task AnOversizedNeighbourIsNotKept()
    {
        // 4096 x 4096 x 4 bytes is 64MB, so a pair cannot fit the budget.
        services.ImageLoader
            .LoadPictureAsync(Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(PictureLoad.Loaded(Picture(4096))));

        PostSummary[] pictures = Pictures(3);
        using ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[0]);
        await viewer.LoadAsync();

        await viewer.ShowNextAsync();

        // Fetched once as a prefetch, dropped, then fetched again on arrival.
        await services.ImageLoader.Received(2).LoadPictureAsync(
            Arg.Is<WebLink>(link => link.Value == pictures[1].Post.FullImage!.Value.Value),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [AvaloniaTest]
    public async Task DisposingTheViewerReleasesThePrefetchToo()
    {
        PostSummary[] pictures = Pictures(3);
        ImageViewerViewModel viewer = Viewer(new StubGallery(pictures), pictures[0]);
        await viewer.LoadAsync();

        Assert.That(viewer.Dispose, Throws.Nothing);
    }
}
