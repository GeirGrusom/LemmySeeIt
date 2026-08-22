namespace Lemmy.Domain.Models;

/// <summary>
/// A post with everything a feed row needs already joined on: who wrote it, where it lives and how
/// it has been voted. Lemmy returns these three together, and keeping them together means the UI
/// never has to fetch a name mid-scroll.
/// </summary>
/// <param name="Post">The post itself.</param>
/// <param name="Creator">Who posted it.</param>
/// <param name="Community">Where it was posted.</param>
/// <param name="Tally">Its vote and comment counts.</param>
/// <param name="CreatorIsModerator">Whether the author moderates the community.</param>
/// <param name="CreatorIsAdmin">Whether the author administers the instance.</param>
/// <param name="MyVote">How the signed-in account voted; <see cref="Vote.None"/> when signed out.</param>
public sealed record PostSummary(
    Post Post,
    Person Creator,
    Community Community,
    PostTally Tally,
    bool CreatorIsModerator,
    bool CreatorIsAdmin,
    Vote MyVote = Vote.None)
{
    /// <summary>Convenience accessor for the post's identifier.</summary>
    public PostId Id => Post.Id;
}
