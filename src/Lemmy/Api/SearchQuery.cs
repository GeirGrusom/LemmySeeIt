using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Api;

/// <summary>What to search for.</summary>
/// <param name="Term">The text to look for.</param>
/// <param name="Kind">What to search through.</param>
/// <param name="Listing">Which slice of the fediverse to search.</param>
/// <param name="Sort">How to order the result.</param>
/// <param name="Community">A single community to restrict the search to.</param>
/// <param name="Page">Which one-based page of results to fetch.</param>
/// <param name="PageSize">How many results to ask for.</param>
public readonly record struct SearchQuery(
    SearchTerm Term,
    SearchKind Kind = SearchKind.All,
    ListingType Listing = ListingType.All,
    PostSortType Sort = PostSortType.TopAll,
    CommunityId? Community = null,
    int Page = 1,
    PageSize PageSize = default)
{
    /// <summary>The page number, floored at one so a partly-filled query is still valid.</summary>
    public int PageNumber => Math.Max(1, Page);
}
