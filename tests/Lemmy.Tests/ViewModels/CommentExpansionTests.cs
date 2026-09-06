using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// Fetching the replies a thread was cut off before reaching. Lemmy answers a whole thread in one
/// request but only down to a fixed depth, so a long back-and-forth arrives truncated and the rest
/// has to be asked for from the comment it was cut at.
/// </summary>
[TestFixture]
internal sealed class CommentExpansionTests
{
    /// <summary>Comment 1, with reply 2 already on screen and two more the server kept back.</summary>
    private static CommentNode Truncated() =>
        Sample.CommentNode(1, "0.1", 3, Sample.CommentNode(2, "0.1.2"));

    private static ILemmyApi ApiReturning(params CommentNode[] arrivals)
    {
        ILemmyApi api = Threads.AnonymousApi();
        api.GetCommentsAsync(Arg.Any<CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Threads.SubThread(arrivals)));
        return api;
    }

    [Test]
    public void ACommentTheServerHeldRepliesBackFromOffersToFetchThem()
    {
        var comment = new CommentViewModel(Truncated(), Threads.Context());

        Assert.Multiple(() =>
        {
            Assert.That(comment.HasUnloadedReplies, Is.True);
            Assert.That(comment.UnloadedRepliesLabel, Is.EqualTo("Show 2 more replies"));
        });
    }

    /// <summary>
    /// The request is anchored on the comment, not on the post: depth counts from there, so this
    /// reaches as far below the cut as the first request reached below the post.
    /// </summary>
    [Test]
    public async Task TheFetchAsksForTheSubThreadBelowThatOneComment()
    {
        ILemmyApi api = ApiReturning();
        var comment = new CommentViewModel(Truncated(), Threads.Context(api, sort: CommentSortType.Top));

        await comment.LoadMoreRepliesCommand.ExecuteAsync(null);

        await api.Received(1).GetCommentsAsync(
            Arg.Is<CommentQuery>(query =>
                query.Parent == new CommentId(1)
                && query.Post == new PostId(10)
                && query.Sort == CommentSortType.Top),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The sub-thread arrives flat and in the server's sort order, which is not tree order: a reply
    /// can turn up before the comment it hangs off. Only the paths say where anything belongs.
    /// </summary>
    [Test]
    public async Task RepliesAreFiledUnderTheirParentWhicheverOrderTheyArriveIn()
    {
        ILemmyApi api = ApiReturning(
            Threads.Reply(4, "0.1.2.3.4"),
            Threads.Reply(3, "0.1.2.3"));

        var comment = new CommentViewModel(Truncated(), Threads.Context(api));
        await comment.LoadMoreRepliesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(Threads.LoadedBelow(comment).Select(id => id.Value), Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(comment.Replies.Single().Replies.Single().Node.Id, Is.EqualTo(new CommentId(3)));
        });
    }

    /// <summary>A fetch always answers with the comment it was asked about, which is already here.</summary>
    [Test]
    public async Task TheCommentAskedAboutIsNotAddedBeneathItself()
    {
        ILemmyApi api = ApiReturning(
            Sample.CommentNode(1, "0.1", 3),
            Threads.Reply(3, "0.1.2.3"));

        var comment = new CommentViewModel(Truncated(), Threads.Context(api));
        await comment.LoadMoreRepliesCommand.ExecuteAsync(null);

        Assert.That(Threads.LoadedBelow(comment).Select(id => id.Value), Is.EqualTo(new[] { 2, 3 }));
    }

    /// <summary>A sub-thread overlaps what is on screen; the overlap must not double up.</summary>
    [Test]
    public async Task RepliesAlreadyOnScreenAreNotAddedASecondTime()
    {
        ILemmyApi api = ApiReturning(
            Threads.Reply(2, "0.1.2"),
            Threads.Reply(3, "0.1.2.3"));

        var comment = new CommentViewModel(Truncated(), Threads.Context(api));
        await comment.LoadMoreRepliesCommand.ExecuteAsync(null);

        Assert.That(Threads.LoadedBelow(comment).Select(id => id.Value), Is.EqualTo(new[] { 2, 3 }));
    }

    [Test]
    public async Task TheCountComesDownAsTheMissingRepliesArrive()
    {
        ILemmyApi api = ApiReturning(
            Threads.Reply(3, "0.1.2.3"),
            Threads.Reply(4, "0.1.2.3.4"));

        var comment = new CommentViewModel(Truncated(), Threads.Context(api));
        await comment.LoadMoreRepliesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(comment.Node.UnloadedReplyCount, Is.Zero);
            Assert.That(comment.HasUnloadedReplies, Is.False);
        });
    }

    /// <summary>Only some of what the count promised can be served; the offer stays up for the rest.</summary>
    [Test]
    public async Task APartialAnswerLeavesTheOfferUpForWhatIsStillMissing()
    {
        ILemmyApi api = ApiReturning(Threads.Reply(3, "0.1.2.3"));

        var comment = new CommentViewModel(Truncated(), Threads.Context(api));
        await comment.LoadMoreRepliesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(comment.HasUnloadedReplies, Is.True);
            Assert.That(comment.UnloadedRepliesLabel, Is.EqualTo("Show 1 more reply"));
        });
    }

    /// <summary>
    /// The server counts replies it will not serve — ones a moderator removed, ones from a blocked
    /// account. Offering to fetch those again would fail the same way every time, so the offer goes
    /// away even though the count never reaches zero.
    /// </summary>
    [Test]
    public async Task AFetchThatBringsNothingStopsOfferingRatherThanRepeatingItself()
    {
        ILemmyApi api = ApiReturning(Sample.CommentNode(1, "0.1", 3));

        var comment = new CommentViewModel(Truncated(), Threads.Context(api));
        await comment.LoadMoreRepliesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(comment.Node.UnloadedReplyCount, Is.EqualTo(2));
            Assert.That(comment.HasUnloadedReplies, Is.False);
        });
    }

    /// <summary>
    /// A reply whose parent is in neither the response nor the tree has nowhere to go. Dropping it
    /// is the only honest option: filing it at the top would say it replied to something it did not.
    /// </summary>
    [Test]
    public async Task AReplyWhoseParentNeverArrivedIsDroppedRatherThanMisplaced()
    {
        ILemmyApi api = ApiReturning(
            Threads.Reply(3, "0.1.2.3"),
            Threads.Reply(9, "0.1.2.7.9"));

        var comment = new CommentViewModel(Truncated(), Threads.Context(api));
        await comment.LoadMoreRepliesCommand.ExecuteAsync(null);

        Assert.That(Threads.LoadedBelow(comment).Select(id => id.Value), Is.EqualTo(new[] { 2, 3 }));
    }

    /// <summary>Newly fetched replies are no use behind a fold the reader has to open again.</summary>
    [Test]
    public async Task FetchingRepliesUnfoldsTheCommentSoTheyCanBeSeen()
    {
        ILemmyApi api = ApiReturning(Threads.Reply(3, "0.1.2.3"));

        var comment = new CommentViewModel(Truncated(), Threads.Context(api));
        comment.ToggleCollapsedCommand.Execute(null);

        await comment.LoadMoreRepliesCommand.ExecuteAsync(null);

        Assert.That(comment.IsCollapsed, Is.False);
    }

    /// <summary>
    /// Every comment above the one that was expanded was counting the same missing replies, and
    /// each of them is on screen saying so. Leaving those alone would offer to fetch a reply that
    /// is already visible a few lines below — which is exactly what a real thread on lemmy.world
    /// did before this.
    /// </summary>
    [Test]
    public async Task RepliesFetchedDeepInAThreadComeOffTheCountOfEveryCommentAboveThem()
    {
        ILemmyApi api = ApiReturning(Threads.Reply(3, "0.1.2.3"));

        CommentNode node = Sample.CommentNode(
            1, "0.1", 2, Sample.CommentNode(2, "0.1.2", 1));

        var root = new CommentViewModel(node, Threads.Context(api));
        CommentViewModel cut = root.Replies.Single();

        Assert.That(root.HasUnloadedReplies, Is.True, "the root is missing the same reply the cut is");

        await cut.LoadMoreRepliesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(cut.HasUnloadedReplies, Is.False);
            Assert.That(root.Node.UnloadedReplyCount, Is.Zero);
            Assert.That(root.HasUnloadedReplies, Is.False);
        });
    }

    [Test]
    public async Task AFailedFetchSaysWhyAndKeepsTheOfferUp()
    {
        ILemmyApi api = Threads.AnonymousApi();
        api.GetCommentsAsync(Arg.Any<CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns<CommentThread>(_ => throw new LemmyApiException("Could not reach lemmy.world."));

        var comment = new CommentViewModel(Truncated(), Threads.Context(api));
        await comment.LoadMoreRepliesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(comment.RepliesError, Is.EqualTo("Could not reach lemmy.world."));
            Assert.That(comment.HasUnloadedReplies, Is.True);
            Assert.That(comment.IsLoadingReplies, Is.False);
        });
    }

    /// <summary>
    /// Deep threads are cut at a comment several levels down, so the offer has to work from a reply
    /// as well as from a root.
    /// </summary>
    [Test]
    public async Task TheOfferWorksFromDeepInTheThreadNotJustFromARoot()
    {
        ILemmyApi api = ApiReturning(Threads.Reply(3, "0.1.2.3"));

        CommentNode node = Sample.CommentNode(
            1, "0.1", 2, Sample.CommentNode(2, "0.1.2", 1));

        var comment = new CommentViewModel(node, Threads.Context(api));
        CommentViewModel reply = comment.Replies.Single();

        Assert.That(reply.HasUnloadedReplies, Is.True);

        await reply.LoadMoreRepliesCommand.ExecuteAsync(null);

        await api.Received(1).GetCommentsAsync(
            Arg.Is<CommentQuery>(query => query.Parent == new CommentId(2)),
            Arg.Any<CancellationToken>());
        Assert.That(reply.Replies.Single().Node.Id, Is.EqualTo(new CommentId(3)));
    }
}
