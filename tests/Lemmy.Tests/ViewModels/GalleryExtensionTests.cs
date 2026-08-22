using System.Collections.Immutable;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// Carrying on past the end of what the page had loaded. The page is underneath a full-screen
/// picture and cannot be scrolled, so the viewer has to ask for more itself.
/// </summary>
[TestFixture]
internal sealed class GalleryExtensionTests
{
    private TestServices services = null!;

    [SetUp]
    public void CreateServices() => services = new TestServices();

    /// <summary>A page that hands out a fixed number of further batches on request.</summary>
    private sealed class GrowingGallery : IImageGallery
    {
        private readonly int picturesPerBatch;

        internal GrowingGallery(int startingPictures, int batchesAvailable, int picturesPerBatch = 2)
        {
            Images = [.. Pictures(0, startingPictures)];
            BatchesLeft = batchesAvailable;
            this.picturesPerBatch = picturesPerBatch;
        }

        public ImmutableArray<PostSummary> Images { get; private set; }

        public bool CanLoadMore => BatchesLeft > 0;

        internal int BatchesLeft { get; private set; }

        internal int LoadMoreCalls { get; private set; }

        public Task LoadMoreAsync()
        {
            LoadMoreCalls++;

            if (BatchesLeft > 0)
            {
                BatchesLeft--;
                Images = [.. Images, .. Pictures(Images.Length, picturesPerBatch)];
            }

            return Task.CompletedTask;
        }

        private static IEnumerable<PostSummary> Pictures(int from, int count) =>
            Enumerable.Range(from, count).Select(index => Sample.ImagePostSummary(10 + index, $"Picture {index}"));
    }

    private ImageViewerViewModel Viewer(IImageGallery gallery, PostSummary start) =>
        new(gallery, start, services.ImageLoader, () => { });

    [Test]
    public async Task ReachingTheLastPictureAsksThePageForMore()
    {
        var gallery = new GrowingGallery(startingPictures: 2, batchesAvailable: 2);
        using ImageViewerViewModel viewer = Viewer(gallery, gallery.Images[1]);

        await viewer.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(gallery.LoadMoreCalls, Is.EqualTo(1));
            Assert.That(viewer.CanShowNext, Is.True, "the new pictures are reachable");
            Assert.That(viewer.PositionLabel, Is.EqualTo("2 / 4"));
        });
    }

    [Test]
    public async Task TheCarouselCarriesOnIntoWhatWasFetched()
    {
        var gallery = new GrowingGallery(startingPictures: 2, batchesAvailable: 1);
        using ImageViewerViewModel viewer = Viewer(gallery, gallery.Images[1]);
        await viewer.LoadAsync();

        await viewer.ShowNextAsync();

        Assert.That(viewer.Summary.Id, Is.EqualTo(gallery.Images[2].Id));
    }

    /// <summary>Nothing to ask for in the middle of the page; the pictures are already there.</summary>
    [Test]
    public async Task BeingInTheMiddleAsksForNothing()
    {
        var gallery = new GrowingGallery(startingPictures: 5, batchesAvailable: 2);
        using ImageViewerViewModel viewer = Viewer(gallery, gallery.Images[1]);

        await viewer.LoadAsync();

        Assert.That(gallery.LoadMoreCalls, Is.Zero);
    }

    [Test]
    public async Task AFeedThatHasRunOutIsNotAskedAgain()
    {
        var gallery = new GrowingGallery(startingPictures: 2, batchesAvailable: 0);
        using ImageViewerViewModel viewer = Viewer(gallery, gallery.Images[1]);

        await viewer.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(gallery.LoadMoreCalls, Is.Zero);
            Assert.That(viewer.CanShowNext, Is.False);
        });
    }

    /// <summary>
    /// A batch can be all articles and bring no pictures at all. Asking once would leave the reader
    /// stuck at the end of a feed that has plenty more to show.
    /// </summary>
    [Test]
    public async Task ABatchWithNoPicturesInItIsFollowedByAnother()
    {
        var gallery = new GrowingGallery(startingPictures: 2, batchesAvailable: 3, picturesPerBatch: 0);
        using ImageViewerViewModel viewer = Viewer(gallery, gallery.Images[1]);

        await viewer.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(gallery.LoadMoreCalls, Is.EqualTo(3), "it keeps looking, but not forever");
            Assert.That(viewer.CanShowNext, Is.False);
        });
    }

    [Test]
    public async Task TheIndicatorIsOffOnceTheFetchIsDone()
    {
        var gallery = new GrowingGallery(startingPictures: 2, batchesAvailable: 1);
        using ImageViewerViewModel viewer = Viewer(gallery, gallery.Images[1]);

        await viewer.LoadAsync();

        Assert.That(viewer.IsLoadingMore, Is.False);
    }

    /// <summary>The feed itself is the gallery, and it knows when it has reached the end.</summary>
    [Test]
    public async Task AFeedStopsOfferingMoreOnceItHasRunOut()
    {
        services.FeedReturns(Sample.MixedPage(imageCount: 2), Sample.PostPage(0, nextCursor: null));
        using var feed = new FeedViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);

        await feed.LoadAsync();
        Assert.That(feed.CanLoadMore, Is.True);

        await feed.LoadMoreAsync();

        Assert.That(feed.CanLoadMore, Is.False);
    }

    [Test]
    public async Task AFeedGrowsItsGalleryWhenAskedForMore()
    {
        services.FeedReturns(Sample.MixedPage(imageCount: 2), Sample.MixedPage(imageCount: 3, nextCursor: null));
        using var feed = new FeedViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await feed.LoadAsync();
        int before = feed.Images.Length;

        await feed.LoadMoreAsync();

        Assert.That(feed.Images, Has.Length.GreaterThan(before));
    }
}
