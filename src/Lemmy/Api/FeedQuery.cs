using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Api;

/// <summary>
/// What to ask a feed for. Every member has a usable <see langword="default"/>, so
/// <c>new FeedQuery()</c> is the instance front page and each variation is one <c>with</c> away.
/// </summary>
/// <param name="Listing">Which slice of the instance to draw from.</param>
/// <param name="Sort">How to order the result.</param>
/// <param name="Community">A single community to restrict the feed to, or <see langword="null"/> for the whole instance.</param>
/// <param name="Cursor">Where to resume, or <see langword="null"/> for the first page.</param>
/// <param name="PageSize">How many posts to ask for.</param>
/// <param name="ShowNsfw">Whether to include communities and posts flagged not safe for work.</param>
public readonly record struct FeedQuery(
    ListingType Listing = ListingType.All,
    PostSortType Sort = PostSortType.Active,
    CommunityId? Community = null,
    PageCursor? Cursor = null,
    PageSize PageSize = default,
    bool ShowNsfw = false)
{
    /// <summary>The same query pointed at the next page.</summary>
    public FeedQuery Next(PageCursor cursor) => this with { Cursor = cursor };

    /// <summary>The same query rewound to the first page, for a pull-to-refresh.</summary>
    public FeedQuery Rewound() => this with { Cursor = null };
}
