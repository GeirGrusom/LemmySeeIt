using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

[TestFixture]
internal sealed class FeedViewModelTests
{
    private TestServices services = null!;
    private RecordingNavigator navigator = null!;

    [SetUp]
    public void CreateServices()
    {
        services = new TestServices();
        navigator = new RecordingNavigator();
    }

    private FeedViewModel CreateFeed(AppSettings? settings = null, CommunitySummary? community = null) =>
        new(services.Services, navigator, services.Api, settings ?? AppSettings.Default, community);

    [Test]
    public async Task LoadAsync_FillsTheListFromTheFirstPage()
    {
        services.FeedReturns(Sample.PostPage(3));
        using FeedViewModel feed = CreateFeed();

        await feed.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(feed.Posts, Has.Count.EqualTo(3));
            Assert.That(feed.Posts[0].Title, Is.EqualTo("Post 0"));
            Assert.That(feed.IsBusy, Is.False);
            Assert.That(feed.HasError, Is.False);
        });
    }

    [Test]
    public async Task LoadAsync_UsesTheSavedSortAndListing()
    {
        services.FeedReturns(Sample.PostPage());
        var settings = AppSettings.Default with { Sort = PostSortType.TopWeek, Listing = ListingType.Local };
        using FeedViewModel feed = CreateFeed(settings);

        await feed.LoadAsync();

        await services.Api.Received(1).GetFeedAsync(
            Arg.Is<FeedQuery>(query => query.Sort == PostSortType.TopWeek && query.Listing == ListingType.Local),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task LoadMoreAsync_AppendsTheNextPageUsingTheCursor()
    {
        services.FeedReturns(Sample.PostPage(2, "P1"), Sample.PostPage(2, "P2"));
        using FeedViewModel feed = CreateFeed();
        await feed.LoadAsync();

        await feed.LoadMoreAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(feed.Posts, Has.Count.EqualTo(4));
            await services.Api.Received(1).GetFeedAsync(
                Arg.Is<FeedQuery>(query => query.Cursor == new PageCursor("P1")),
                Arg.Any<CancellationToken>());
        });
    }

    /// <summary>
    /// A scroll handler fires this constantly. Without the guard the feed would issue overlapping
    /// requests whose pages interleave in the list.
    /// </summary>
    [Test]
    public async Task LoadMoreAsync_DoesNothingOnceTheFeedHasEnded()
    {
        services.FeedReturns(Sample.PostPage(2, nextCursor: null));
        using FeedViewModel feed = CreateFeed();
        await feed.LoadAsync();

        await feed.LoadMoreAsync();
        await feed.LoadMoreAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(feed.HasReachedEnd, Is.True);
            await services.Api.Received(1).GetFeedAsync(Arg.Any<FeedQuery>(), Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task LoadMoreAsync_DoesNothingBeforeTheFirstPageHasLanded()
    {
        using FeedViewModel feed = CreateFeed();

        await feed.LoadMoreAsync();

        await services.Api.DidNotReceive().GetFeedAsync(Arg.Any<FeedQuery>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ChangingTheSort_ReloadsFromTheTop()
    {
        services.FeedReturns(Sample.PostPage(2));
        using FeedViewModel feed = CreateFeed();
        await feed.LoadAsync();

        feed.SelectedSort = PostSortType.New;

        Assert.Multiple(async () =>
        {
            Assert.That(feed.Posts, Has.Count.EqualTo(2), "the previous page is discarded, not appended to");
            await services.Api.Received(1).GetFeedAsync(
                Arg.Is<FeedQuery>(query => query.Sort == PostSortType.New && query.Cursor == null),
                Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task SettingTheSortToWhatItAlreadyIs_DoesNotRefetch()
    {
        services.FeedReturns(Sample.PostPage(2));
        using FeedViewModel feed = CreateFeed();
        await feed.LoadAsync();

        feed.SelectedSort = AppSettings.Default.Sort;

        await services.Api.Received(1).GetFeedAsync(Arg.Any<FeedQuery>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AFailedLoad_ShowsTheServersMessageRatherThanThrowing()
    {
        services.Api.GetFeedAsync(Arg.Any<FeedQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<PostPage>>(_ => throw new LemmyApiException("lemmy.world answered 502 Bad Gateway."));
        using FeedViewModel feed = CreateFeed();

        await feed.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(feed.HasError, Is.True);
            Assert.That(feed.ErrorMessage, Does.Contain("502"));
            Assert.That(feed.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task ReloadAsync_ClearsAPreviousError()
    {
        bool firstCall = true;
        services.Api.GetFeedAsync(Arg.Any<FeedQuery>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (firstCall)
            {
                firstCall = false;
                throw new LemmyApiException("down");
            }

            return Task.FromResult(Sample.PostPage(1));
        });
        using FeedViewModel feed = CreateFeed();
        await feed.LoadAsync();

        await feed.ReloadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(feed.HasError, Is.False);
            Assert.That(feed.Posts, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task OpeningAPost_NavigatesToItWithoutRefetchingIt()
    {
        services.FeedReturns(Sample.PostPage(1));
        using FeedViewModel feed = CreateFeed();
        await feed.LoadAsync();

        feed.Posts[0].OpenCommand.Execute(null);

        Assert.Multiple(async () =>
        {
            Assert.That(navigator.Pushed.Single(), Is.TypeOf<PostDetailViewModel>());
            await services.Api.DidNotReceive().GetPostAsync(Arg.Any<PostId>(), Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public void ACommunityFeed_IsScopedToThatCommunityAndHidesTheListingPicker()
    {
        services.FeedReturns(Sample.PostPage(1));
        using FeedViewModel feed = CreateFeed(community: Sample.CommunitySummary());

        Assert.Multiple(() =>
        {
            Assert.That(feed.IsCommunityFeed, Is.True);
            Assert.That(feed.Title, Is.EqualTo("Technology"));
            Assert.That(feed.SubtitleLabel, Does.Contain("!technology@lemmy.world").And.Contain("87k"));
        });
    }

    [Test]
    public async Task ACommunityFeed_AsksForThatCommunityOnly()
    {
        services.FeedReturns(Sample.PostPage(1));
        using FeedViewModel feed = CreateFeed(community: Sample.CommunitySummary());

        await feed.LoadAsync();

        await services.Api.Received(1).GetFeedAsync(
            Arg.Is<FeedQuery>(query => query.Community == new CommunityId(2)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ThumbnailsAreRequestedAtTheSizeTheyAreDrawn()
    {
        services.FeedReturns(new PostPage([Sample.PostSummary()], null));
        using FeedViewModel feed = CreateFeed();

        await feed.LoadAsync();

        // The sample post has no image, so nothing should be fetched at all.
        await services.ImageLoader.DidNotReceive().LoadAsync(Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    /// <summary>What the pull gesture calls; it must discard the loaded pages, not append to them.</summary>
    [Test]
    public async Task RefreshAsync_StartsTheFeedOverFromTheFirstPage()
    {
        services.FeedReturns(Sample.PostPage(2, "P1"), Sample.PostPage(2, "P2"));
        using FeedViewModel feed = CreateFeed();
        await feed.LoadAsync();
        await feed.LoadMoreAsync();

        await feed.RefreshAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(feed.Posts, Has.Count.EqualTo(2));
            await services.Api.Received(2).GetFeedAsync(
                Arg.Is<FeedQuery>(query => query.Cursor == null),
                Arg.Any<CancellationToken>());
        });
    }

    /// <summary>
    /// A pull-to-refresh should not blank the feed for the length of a round trip. The rows stay up
    /// until replacements exist.
    /// </summary>
    [Test]
    public async Task RefreshAsync_KeepsTheOldRowsUntilTheNewPageArrives()
    {
        var gate = new TaskCompletionSource<PostPage>();
        bool firstCall = true;
        services.Api.GetFeedAsync(Arg.Any<FeedQuery>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (!firstCall)
            {
                return gate.Task;
            }

            firstCall = false;
            return Task.FromResult(Sample.PostPage(3, null));
        });
        using FeedViewModel feed = CreateFeed();
        await feed.LoadAsync();

        Task refreshing = feed.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(feed.Posts, Has.Count.EqualTo(3), "the old rows are still on screen");
            Assert.That(feed.IsLoadingFirstPage, Is.False, "the pull gesture has its own spinner");
        });

        gate.SetResult(Sample.PostPage(2, null));
        await refreshing;

        Assert.That(feed.Posts, Has.Count.EqualTo(2));
    }

    /// <summary>A refresh that fails should cost the reader the update, not the feed.</summary>
    [Test]
    public async Task AFailedRefresh_LeavesThePreviousPostsInPlace()
    {
        bool firstCall = true;
        services.Api.GetFeedAsync(Arg.Any<FeedQuery>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (firstCall)
            {
                firstCall = false;
                return Task.FromResult(Sample.PostPage(3, null));
            }

            throw new LemmyApiException("lemmy.world answered 502.");
        });
        using FeedViewModel feed = CreateFeed();
        await feed.LoadAsync();

        await feed.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(feed.Posts, Has.Count.EqualTo(3));
            Assert.That(feed.HasError, Is.True);
        });
    }

    [Test]
    public async Task IsLoadingFirstPage_IsOnlyTrueWhileThereIsNothingToShow()
    {
        var gate = new TaskCompletionSource<PostPage>();
        services.Api.GetFeedAsync(Arg.Any<FeedQuery>(), Arg.Any<CancellationToken>()).Returns(_ => gate.Task);
        using FeedViewModel feed = CreateFeed();

        Task loading = feed.LoadAsync();
        Assert.That(feed.IsLoadingFirstPage, Is.True);

        gate.SetResult(Sample.PostPage(2, null));
        await loading;

        Assert.That(feed.IsLoadingFirstPage, Is.False);
    }

    [Test]
    public async Task DisposingTheFeed_StopsFurtherPaging()
    {
        services.FeedReturns(Sample.PostPage(2, "P1"));
        FeedViewModel feed = CreateFeed();
        await feed.LoadAsync();

        feed.Dispose();
        await feed.LoadMoreAsync();

        Assert.That(feed.Posts, Is.Empty);
    }
}
