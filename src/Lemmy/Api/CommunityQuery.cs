using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Api;

/// <summary>What to ask a community listing for.</summary>
/// <param name="Listing">Which slice of the fediverse to draw from.</param>
/// <param name="Sort">How to order the result; the <c>Top*</c> sorts rank by subscriber count.</param>
/// <param name="Page">Which one-based page to fetch — community listings page by number, not cursor.</param>
/// <param name="PageSize">How many communities to ask for.</param>
/// <param name="ShowNsfw">Whether to include communities flagged not safe for work.</param>
public readonly record struct CommunityQuery(
    ListingType Listing = ListingType.Local,
    PostSortType Sort = PostSortType.TopMonth,
    int Page = 1,
    PageSize PageSize = default,
    bool ShowNsfw = false)
{
    /// <summary>The page number, floored at one so a <see langword="default"/> query is still valid.</summary>
    public int PageNumber => Math.Max(1, Page);
}
