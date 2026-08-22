using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// The bar moves before the server has answered and has to put things back if the answer never
/// comes, which is the part worth testing: a vote that silently half-applied would leave the reader
/// looking at a score that is wrong until they reload.
/// </summary>
[TestFixture]
internal sealed class VoteBarViewModelTests
{
    private static VoteOutcome Start(Vote mine = Vote.None, int score = 10) =>
        new(mine, new Score(score), new VoteCount(12), new VoteCount(2));

    private static VoteBarViewModel Bar(
        Func<Vote, CancellationToken, Task<VoteOutcome>> cast,
        Vote mine = Vote.None,
        bool canVote = true) =>
        new(Start(mine), canVote, cast);

    [Test]
    public async Task AnUpvoteShowsBeforeTheServerHasAnswered()
    {
        var gate = new TaskCompletionSource<VoteOutcome>();
        VoteBarViewModel bar = Bar((_, _) => gate.Task);

        Task voting = bar.UpvoteCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(bar.IsUpvoted, Is.True, "the arrow should light immediately");
            Assert.That(bar.ScoreLabel, Is.EqualTo("11"));
            Assert.That(bar.IsVoting, Is.True);
        });

        gate.SetResult(new VoteOutcome(Vote.Up, new Score(11), new VoteCount(13), new VoteCount(2)));
        await voting;
    }

    [Test]
    public async Task TheServersOwnCountsWinOnceTheyArrive()
    {
        // Other people were voting at the same time, so the guess of 11 is not what came back.
        VoteBarViewModel bar = Bar((_, _) =>
            Task.FromResult(new VoteOutcome(Vote.Up, new Score(87), new VoteCount(90), new VoteCount(3))));

        await bar.UpvoteCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(bar.ScoreLabel, Is.EqualTo("87"));
            Assert.That(bar.IsUpvoted, Is.True);
            Assert.That(bar.IsVoting, Is.False);
        });
    }

    [Test]
    public async Task AFailedVoteIsPutBackExactlyAsItWas()
    {
        VoteBarViewModel bar = Bar((_, _) => throw new LemmyApiException("Could not reach lemmy.world."));

        await bar.UpvoteCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(bar.IsUpvoted, Is.False);
            Assert.That(bar.ScoreLabel, Is.EqualTo("10"));
            Assert.That(bar.ErrorMessage, Is.EqualTo("Could not reach lemmy.world."));
        });
    }

    [Test]
    public async Task PressingTheSameArrowAgainRetractsTheVote()
    {
        Vote? sent = null;
        VoteBarViewModel bar = Bar(
            (vote, _) =>
            {
                sent = vote;
                return Task.FromResult(new VoteOutcome(vote, new Score(10), new VoteCount(12), new VoteCount(2)));
            },
            mine: Vote.Up,
            canVote: true);

        await bar.UpvoteCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(sent, Is.EqualTo(Vote.None));
            Assert.That(bar.IsUpvoted, Is.False);
        });
    }

    [Test]
    public async Task SwitchingSidesSendsTheNewVoteRatherThanTwoRequests()
    {
        var sent = new List<Vote>();
        VoteBarViewModel bar = Bar(
            (vote, _) =>
            {
                sent.Add(vote);
                return Task.FromResult(new VoteOutcome(vote, new Score(8), new VoteCount(12), new VoteCount(4)));
            },
            mine: Vote.Up);

        await bar.DownvoteCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(sent, Is.EqualTo(new[] { Vote.Down }));
            Assert.That(bar.IsDownvoted, Is.True);
            Assert.That(bar.IsUpvoted, Is.False);
        });
    }

    [Test]
    public void SignedOutThereIsNothingToPress()
    {
        VoteBarViewModel bar = Bar((_, _) => Task.FromResult(Start()), canVote: false);

        Assert.Multiple(() =>
        {
            Assert.That(bar.CanVote, Is.False);
            Assert.That(bar.UpvoteCommand.CanExecute(null), Is.False);
            Assert.That(bar.DownvoteCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void TheArrowsAreClosedWhileAVoteIsInFlight()
    {
        var gate = new TaskCompletionSource<VoteOutcome>();
        VoteBarViewModel bar = Bar((_, _) => gate.Task);

        _ = bar.UpvoteCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(bar.UpvoteCommand.CanExecute(null), Is.False);
            Assert.That(bar.DownvoteCommand.CanExecute(null), Is.False);
        });

        gate.SetResult(Start(Vote.Up, 11));
    }

    [Test]
    public async Task AnEarlierFailureIsClearedWhenTheNextVoteWorks()
    {
        bool fail = true;
        VoteBarViewModel bar = Bar((vote, _) => fail
            ? throw new LemmyApiException("Could not reach lemmy.world.")
            : Task.FromResult(new VoteOutcome(vote, new Score(11), new VoteCount(13), new VoteCount(2))));

        await bar.UpvoteCommand.ExecuteAsync(null);
        Assert.That(bar.ErrorMessage, Is.Not.Null);

        fail = false;
        await bar.UpvoteCommand.ExecuteAsync(null);

        Assert.That(bar.ErrorMessage, Is.Null);
    }
}
