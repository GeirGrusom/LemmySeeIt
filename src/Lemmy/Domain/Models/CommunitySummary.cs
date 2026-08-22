namespace Lemmy.Domain.Models;

/// <summary>A community with its activity counts joined on.</summary>
/// <param name="Community">The community itself.</param>
/// <param name="Tally">How busy it is.</param>
public sealed record CommunitySummary(Community Community, CommunityTally Tally)
{
    /// <summary>Convenience accessor for the community's identifier.</summary>
    public CommunityId Id => Community.Id;
}
