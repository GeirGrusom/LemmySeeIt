using Lemmy.Domain;

namespace Lemmy.Tests.Domain;

/// <summary>
/// The arithmetic behind an arrow lighting up before the server has answered. It has to be right in
/// both directions, because switching from up to down moves the score by two, not one.
/// </summary>
[TestFixture]
internal sealed class VoteOutcomeTests
{
    private static VoteOutcome Outcome(Vote mine, int score, int up, int down) =>
        new(mine, new Score(score), new VoteCount(up), new VoteCount(down));

    [Test]
    public void UpvotingFromNothingAddsOneToTheScoreAndTheUpvotes()
    {
        VoteOutcome after = Outcome(Vote.None, 10, 12, 2).WithVote(Vote.Up);

        Assert.Multiple(() =>
        {
            Assert.That(after.MyVote, Is.EqualTo(Vote.Up));
            Assert.That(after.Score.Value, Is.EqualTo(11));
            Assert.That(after.Upvotes.Value, Is.EqualTo(13));
            Assert.That(after.Downvotes.Value, Is.EqualTo(2));
        });
    }

    [Test]
    public void SwitchingFromUpToDownMovesTheScoreByTwo()
    {
        VoteOutcome after = Outcome(Vote.Up, 11, 13, 2).WithVote(Vote.Down);

        Assert.Multiple(() =>
        {
            Assert.That(after.Score.Value, Is.EqualTo(9));
            Assert.That(after.Upvotes.Value, Is.EqualTo(12));
            Assert.That(after.Downvotes.Value, Is.EqualTo(3));
        });
    }

    [Test]
    public void TakingAnUpvoteBackUndoesExactlyWhatCastingItDid()
    {
        VoteOutcome start = Outcome(Vote.None, 10, 12, 2);

        Assert.That(start.WithVote(Vote.Up).WithVote(Vote.None), Is.EqualTo(start));
    }

    [Test]
    public void RepeatingTheSameVoteChangesNothing()
    {
        VoteOutcome start = Outcome(Vote.Up, 11, 13, 2);

        Assert.That(start.WithVote(Vote.Up), Is.EqualTo(start));
    }

    [Test]
    public void AScoreCanGoNegativeButACountCannot()
    {
        // A tally that was already stale can be asked to drop below zero; the score is signed and
        // may, the counts are not and may not.
        VoteOutcome after = Outcome(Vote.Up, 0, 0, 0).WithVote(Vote.Down);

        Assert.Multiple(() =>
        {
            Assert.That(after.Score.Value, Is.EqualTo(-2));
            Assert.That(after.Upvotes.Value, Is.Zero);
            Assert.That(after.Downvotes.Value, Is.EqualTo(1));
        });
    }

    [TestCase(Vote.None, Vote.Up, Vote.Up)]
    [TestCase(Vote.Up, Vote.Up, Vote.None)]
    [TestCase(Vote.Down, Vote.Up, Vote.Up)]
    [TestCase(Vote.Down, Vote.Down, Vote.None)]
    public void PressingAnArrowYouAlreadyChoseTakesTheVoteBack(Vote current, Vote pressed, Vote expected) =>
        Assert.That(current.Toggle(pressed), Is.EqualTo(expected));

    [TestCase(Vote.Up, 1)]
    [TestCase(Vote.None, 0)]
    [TestCase(Vote.Down, -1)]
    public void TheWireScoreIsTheEnumValue(Vote vote, int expected) =>
        Assert.That(vote.ToScore(), Is.EqualTo(expected));
}
