using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

[TestFixture]
internal sealed class PostDetailViewModelTests
{
    private TestServices services = null!;
    private RecordingNavigator navigator = null!;

    [SetUp]
    public void CreateServices()
    {
        services = new TestServices();
        navigator = new RecordingNavigator();
        ThreadReturns(CommentThread.Empty);
    }

    private void ThreadReturns(CommentThread thread) =>
        services.Api.GetCommentsAsync(Arg.Any<CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(thread));

    /// <summary>
    /// A page whose "wait for the frame" step returns at once. Nothing is turning the dispatcher in
    /// a unit test, so the real one would wait for a frame that never comes.
    /// </summary>
    private PostDetailViewModel CreatePage(PostSummary? summary = null, AppSettings? settings = null) =>
        new(services.Services, navigator, services.Api, summary ?? Sample.PostSummary(), settings ?? AppSettings.Default)
        {
            Drawn = () => Task.CompletedTask,
        };

    /// <summary>A thread of <paramref name="count"/> top-level comments.</summary>
    private static CommentThread ThreadOf(int count) =>
        new([.. Enumerable.Range(1, count).Select(id => Sample.CommentNode(id, $"0.{id}"))]);

    /// <summary>The gesture is about the conversation, so it re-reads the thread and nothing else.</summary>
    [Test]
    public async Task RefreshAsync_RefetchesTheThread()
    {
        using PostDetailViewModel page = CreatePage();
        await page.LoadAsync();

        await page.RefreshAsync();

        await services.Api.Received(2).GetCommentsAsync(
            Arg.Is<CommentQuery>(query => query.Post == new PostId(10)),
            Arg.Any<CancellationToken>());
        await services.Api.DidNotReceive().GetPostAsync(Arg.Any<PostId>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A pull-to-refresh should not blank the thread for the length of a round trip. The comments
    /// stay up until replacements exist.
    /// </summary>
    [Test]
    public async Task RefreshAsync_KeepsTheOldThreadUntilTheNewOneArrives()
    {
        var gate = new TaskCompletionSource<CommentThread>();
        bool firstCall = true;
        services.Api.GetCommentsAsync(Arg.Any<CommentQuery>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (!firstCall)
            {
                return gate.Task;
            }

            firstCall = false;
            return Task.FromResult(new CommentThread([Sample.CommentNode(100, "0.100"), Sample.CommentNode(300, "0.300")]));
        });
        using PostDetailViewModel page = CreatePage();
        await page.LoadAsync();

        Task refreshing = page.RefreshAsync();

        Assert.That(page.Comments, Has.Count.EqualTo(2), "the old thread is still on screen");

        gate.SetResult(new CommentThread([Sample.CommentNode(400, "0.400")]));
        await refreshing;

        Assert.That(page.Comments.Single().Node.Id, Is.EqualTo(new CommentId(400)));
    }

    /// <summary>A refresh that fails should cost the reader the update, not the thread.</summary>
    [Test]
    public async Task RefreshAsync_LeavesTheThreadAloneWhenItFails()
    {
        ThreadReturns(new CommentThread([Sample.CommentNode(100, "0.100")]));
        using PostDetailViewModel page = CreatePage();
        await page.LoadAsync();

        services.Api.GetCommentsAsync(Arg.Any<CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns<CommentThread>(_ => throw new LemmyApiException("lemmy.world answered 502."));

        await page.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.Comments, Has.Count.EqualTo(1));
            Assert.That(page.HasNoComments, Is.False, "a failed refresh does not mean there are no comments");
            Assert.That(page.ErrorMessage, Is.EqualTo("lemmy.world answered 502."));
        });
    }

    /// <summary>
    /// A busy thread is hundreds of nested controls, and handing them all to the layout at once
    /// freezes a phone for over a second — measured at 1.3s for a 179-comment thread on a Galaxy
    /// S24. They go on in screenfuls instead, and every one of them still arrives.
    /// </summary>
    [Test]
    public async Task LoadAsync_PutsALongThreadOnScreenInBatches()
    {
        ThreadReturns(ThreadOf(40));
        var batches = 0;
        using PostDetailViewModel page = CreatePage();
        page.Drawn = () =>
        {
            batches++;
            return Task.CompletedTask;
        };

        await page.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.Comments, Has.Count.EqualTo(40), "every comment still arrives");
            Assert.That(batches, Is.GreaterThan(1), "and not all in one pass");
        });
    }

    /// <summary>The first screenful is on screen before the layout is asked for any more than that.</summary>
    [Test]
    public async Task LoadAsync_ShowsTheFirstCommentsBeforeTheRest()
    {
        ThreadReturns(ThreadOf(40));
        var countAtFirstDraw = 0;
        using PostDetailViewModel page = CreatePage();
        page.Drawn = () =>
        {
            countAtFirstDraw = countAtFirstDraw == 0 ? page.Comments.Count : countAtFirstDraw;
            return Task.CompletedTask;
        };

        await page.LoadAsync();

        Assert.That(countAtFirstDraw, Is.InRange(1, 12), "a screenful, not the whole thread");
    }

    /// <summary>
    /// Filling runs across several turns of the dispatcher, so a sort changed or a refresh pulled
    /// midway has to abandon the thread before it rather than interleave the two into one list.
    /// </summary>
    [Test]
    public async Task ASecondLoadStartedMidwayTakesOverTheList()
    {
        ThreadReturns(ThreadOf(40));
        using PostDetailViewModel page = CreatePage();

        Task? second = null;
        page.Drawn = () =>
        {
            // Start the replacement once, from inside the first one's filling.
            if (second is null)
            {
                ThreadReturns(ThreadOf(3));
                second = page.ReloadCommentsAsync();
            }

            return Task.CompletedTask;
        };

        await page.LoadAsync();
        await second!;

        Assert.That(page.Comments, Has.Count.EqualTo(3), "the second thread owns the list");
    }

    [Test]
    public async Task LoadAsync_FetchesTheThreadForThatPost()
    {
        using PostDetailViewModel page = CreatePage();

        await page.LoadAsync();

        await services.Api.Received(1).GetCommentsAsync(
            Arg.Is<CommentQuery>(query => query.Post == new PostId(10)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task LoadAsync_BuildsAViewModelPerRootComment()
    {
        ThreadReturns(new CommentThread(
        [
            Sample.CommentNode(100, "0.100", 1, Sample.CommentNode(200, "0.100.200")),
            Sample.CommentNode(300, "0.300"),
        ]));
        using PostDetailViewModel page = CreatePage();

        await page.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.Comments, Has.Count.EqualTo(2));
            Assert.That(page.Comments[0].Replies, Has.Count.EqualTo(1));
            Assert.That(page.HasNoComments, Is.False);
        });
    }

    [Test]
    public async Task LoadAsync_SaysSoWhenThereAreNoComments()
    {
        using PostDetailViewModel page = CreatePage();

        await page.LoadAsync();

        Assert.That(page.HasNoComments, Is.True);
    }

    [Test]
    public async Task ChangingTheCommentSort_RefetchesTheThread()
    {
        using PostDetailViewModel page = CreatePage();
        await page.LoadAsync();

        page.SelectedSort = CommentSortType.New;

        await services.Api.Received(1).GetCommentsAsync(
            Arg.Is<CommentQuery>(query => query.Sort == CommentSortType.New),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void TheStartingCommentSortIsTheOneTheReaderChose()
    {
        using PostDetailViewModel page = CreatePage(settings: AppSettings.Default with { CommentSort = CommentSortType.Top });

        Assert.That(page.SelectedSort, Is.EqualTo(CommentSortType.Top));
    }

    [Test]
    public async Task AFailedThread_ShowsTheServersMessageWithoutLosingThePost()
    {
        services.Api.GetCommentsAsync(Arg.Any<CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<CommentThread>>(_ => throw new LemmyApiException("lemmy.world answered 500."));
        using PostDetailViewModel page = CreatePage();

        await page.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.HasError, Is.True);
            Assert.That(page.Title, Is.EqualTo("A post"), "the post itself came from the feed, not the failed call");
        });
    }

    [Test]
    public void OpeningTheCommunity_NavigatesToItsFeed()
    {
        using PostDetailViewModel page = CreatePage();

        page.OpenCommunityCommand.Execute(null);

        Assert.That(navigator.Pushed.Single(), Is.TypeOf<FeedViewModel>());
    }

    [Test]
    public void ANsfwPost_StartsWithItsImageCoveredWhenTheReaderAskedForThat()
    {
        using PostDetailViewModel page = CreatePage(Sample.PostSummary(isNsfw: true));

        Assert.That(page.IsImageHidden, Is.True);

        page.RevealImageCommand.Execute(null);

        Assert.That(page.IsImageHidden, Is.False);
    }

    [Test]
    public void ANsfwPost_IsNotCoveredWhenTheReaderTurnedBlurringOff()
    {
        using PostDetailViewModel page = CreatePage(
            Sample.PostSummary(isNsfw: true),
            AppSettings.Default with { BlurNsfwImages = false });

        Assert.That(page.IsImageHidden, Is.False);
    }
}
