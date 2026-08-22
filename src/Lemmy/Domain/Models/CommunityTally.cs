namespace Lemmy.Domain.Models;

/// <summary>How busy a community is.</summary>
/// <param name="Subscribers">People subscribed from anywhere in the fediverse.</param>
/// <param name="Posts">Posts made in the community.</param>
/// <param name="Comments">Comments made in the community.</param>
/// <param name="UsersActiveMonth">Distinct people who posted or commented in the last month.</param>
public readonly record struct CommunityTally(
    VoteCount Subscribers,
    VoteCount Posts,
    VoteCount Comments,
    VoteCount UsersActiveMonth)
{
    /// <summary>An empty tally.</summary>
    public static CommunityTally Empty => default;
}
