using System.Collections.Immutable;

namespace Lemmy.Domain.Models;

/// <summary>
/// A comment together with its replies. Lemmy sends a thread as a flat list plus a
/// <see cref="CommentPath"/> on each item; assembling that into this shape is what
/// <c>CommentTreeBuilder</c> exists for.
/// </summary>
/// <param name="Comment">The comment itself.</param>
/// <param name="Creator">Who wrote it.</param>
/// <param name="Tally">Its vote and reply counts.</param>
/// <param name="CreatorIsModerator">Whether the author moderates the community.</param>
/// <param name="CreatorIsAdmin">Whether the author administers the instance.</param>
/// <param name="Replies">Direct replies, already ordered.</param>
/// <param name="MyVote">How the signed-in account voted; <see cref="Vote.None"/> when signed out.</param>
public sealed record CommentNode(
    Comment Comment,
    Person Creator,
    CommentTally Tally,
    bool CreatorIsModerator,
    bool CreatorIsAdmin,
    ImmutableArray<CommentNode> Replies,
    Vote MyVote = Vote.None)
{
    /// <summary>Convenience accessor for the comment's identifier.</summary>
    public CommentId Id => Comment.Id;

    /// <summary>How deeply the comment is nested; a direct reply to the post is 1.</summary>
    public int Depth => Comment.Path.Depth;

    /// <summary>
    /// Replies the server said exist but did not send in this response — the "12 more replies"
    /// affordance. Negative differences are clamped away because the two numbers are computed at
    /// slightly different times on the server.
    /// </summary>
    public int UnloadedReplyCount => Math.Max(0, Tally.ChildCount.Value - CountLoadedDescendants());

    private int CountLoadedDescendants()
    {
        int total = Replies.Length;
        foreach (CommentNode reply in Replies)
        {
            total += reply.CountLoadedDescendants();
        }

        return total;
    }
}
