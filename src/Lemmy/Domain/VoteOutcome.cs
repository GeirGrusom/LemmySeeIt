namespace Lemmy.Domain;

/// <summary>
/// What a server said the counts became after a vote was cast. The client guesses the same numbers
/// the moment the arrow is pressed; this is the answer it reconciles against, because other people
/// are voting on the same thing at the same time and the guess is only ever approximately right.
/// </summary>
/// <param name="MyVote">How the account now stands on it.</param>
/// <param name="Score">Upvotes minus downvotes.</param>
/// <param name="Upvotes">Total upvotes.</param>
/// <param name="Downvotes">Total downvotes.</param>
public readonly record struct VoteOutcome(Vote MyVote, Score Score, VoteCount Upvotes, VoteCount Downvotes)
{
    /// <summary>
    /// The counts as they will read once the vote becomes <paramref name="next"/>, so a row can
    /// update the instant the arrow is pressed instead of waiting for a round trip. The score moves
    /// by the difference between the two votes; the upvote and downvote totals move only if their
    /// side was involved.
    /// </summary>
    public VoteOutcome WithVote(Vote next)
    {
        if (next == MyVote)
        {
            return this;
        }

        return this with
        {
            MyVote = next,
            Score = new Score(Score.Value + (next.ToScore() - MyVote.ToScore())),

            // The client's arithmetic and the server's totals can disagree — a count that was
            // already stale goes negative here — and a negative tally is not a thing.
            Upvotes = VoteCount.Clamp(Upvotes.Value + Delta(MyVote, next, Vote.Up)),
            Downvotes = VoteCount.Clamp(Downvotes.Value + Delta(MyVote, next, Vote.Down)),
        };
    }

    private static int Delta(Vote previous, Vote next, Vote side) =>
        (next == side ? 1 : 0) - (previous == side ? 1 : 0);
}
