namespace Lemmy.Domain.Models;

/// <summary>The vote and comment counts that travel alongside a post.</summary>
/// <param name="Score">Upvotes minus downvotes.</param>
/// <param name="Upvotes">Total upvotes.</param>
/// <param name="Downvotes">Total downvotes.</param>
/// <param name="Comments">Total comments, including replies at every depth.</param>
/// <param name="NewestCommentTime">When the most recent comment arrived, if there is one.</param>
public readonly record struct PostTally(
    Score Score,
    VoteCount Upvotes,
    VoteCount Downvotes,
    VoteCount Comments,
    DateTimeOffset? NewestCommentTime)
{
    /// <summary>An empty tally, for a post nobody has touched yet.</summary>
    public static PostTally Empty => default;

    /// <summary>
    /// The share of votes that were upvotes, or <see langword="null"/> when nobody has voted.
    /// Worth showing alongside the score: +5 from five voters reads very differently to +5 from a hundred.
    /// </summary>
    public double? UpvoteRatio
    {
        get
        {
            int total = Upvotes.Value + Downvotes.Value;
            return total == 0 ? null : (double)Upvotes.Value / total;
        }
    }
}
