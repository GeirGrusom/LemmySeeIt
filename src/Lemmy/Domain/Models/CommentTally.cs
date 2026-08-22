namespace Lemmy.Domain.Models;

/// <summary>The vote and reply counts that travel alongside a comment.</summary>
/// <param name="Score">Upvotes minus downvotes.</param>
/// <param name="Upvotes">Total upvotes.</param>
/// <param name="Downvotes">Total downvotes.</param>
/// <param name="ChildCount">Replies below this comment at every depth.</param>
public readonly record struct CommentTally(
    Score Score,
    VoteCount Upvotes,
    VoteCount Downvotes,
    VoteCount ChildCount)
{
    /// <summary>An empty tally, for a comment nobody has touched yet.</summary>
    public static CommentTally Empty => default;
}
