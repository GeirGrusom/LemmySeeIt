using Lemmy.Domain;

namespace Lemmy.Domain.Models;

/// <summary>A community with its activity counts joined on.</summary>
/// <param name="Community">The community itself.</param>
/// <param name="Tally">How busy it is.</param>
/// <param name="Subscription">Whether the signed-in account follows it.</param>
public sealed record CommunitySummary(
    Community Community,
    CommunityTally Tally,
    SubscriptionState Subscription = SubscriptionState.NotSubscribed)
{
    /// <summary>Convenience accessor for the community's identifier.</summary>
    public CommunityId Id => Community.Id;
}
