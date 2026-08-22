using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Services;

/// <summary>
/// Everything the app remembers between runs. Immutable: settings changes flow through
/// <c>with</c> expressions, so a half-applied change can never be what gets written to disk.
/// </summary>
/// <param name="Instance">The server to read from.</param>
/// <param name="Listing">Which slice of the instance the feed shows.</param>
/// <param name="Sort">How the feed is ordered.</param>
/// <param name="CommentSort">How comment threads are ordered.</param>
/// <param name="ShowNsfw">Whether to include content flagged not safe for work.</param>
/// <param name="BlurNsfwImages">Whether to blur such images until tapped, when they are shown at all.</param>
public sealed record AppSettings(
    InstanceAddress Instance,
    ListingType Listing = ListingType.All,
    PostSortType Sort = PostSortType.Active,
    CommentSortType CommentSort = CommentSortType.Hot,
    bool ShowNsfw = false,
    bool BlurNsfwImages = true)
{
    /// <summary>What a first run uses: a large general-purpose instance, safe defaults.</summary>
    public static AppSettings Default { get; } = new(InstanceAddress.Parse("lemmy.world"));
}
