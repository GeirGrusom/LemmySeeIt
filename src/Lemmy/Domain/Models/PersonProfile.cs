using System.Collections.Immutable;

namespace Lemmy.Domain.Models;

/// <summary>How much an account has written.</summary>
/// <param name="Posts">Posts submitted.</param>
/// <param name="Comments">Comments written.</param>
public readonly record struct PersonTally(VoteCount Posts, VoteCount Comments)
{
    /// <summary>An account that has written nothing.</summary>
    public static PersonTally Empty => default;
}

/// <summary>
/// An account's own page: who they are, what they wrote about themselves, and the most recent of
/// what they have written.
/// </summary>
/// <param name="Person">The account.</param>
/// <param name="Bio">What they wrote about themselves, which may be empty.</param>
/// <param name="Banner">Their banner image, when they set one.</param>
/// <param name="Tally">How much they have written.</param>
/// <param name="IsAdmin">Whether they administer the instance.</param>
/// <param name="Posts">Their recent posts, newest first.</param>
/// <param name="Comments">
/// Their recent comments, newest first and each standing alone — this is a list of what somebody
/// wrote, not a thread, so the replies below each one are not part of it.
/// </param>
public sealed record PersonProfile(
    Person Person,
    MarkdownText Bio,
    WebLink? Banner,
    PersonTally Tally,
    bool IsAdmin,
    ImmutableArray<PostSummary> Posts,
    ImmutableArray<CommentNode> Comments)
{
    /// <summary>Whether they wrote anything about themselves.</summary>
    public bool HasBio => !Bio.IsEmpty;
}
