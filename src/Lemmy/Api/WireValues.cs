using Lemmy.Domain.Models;

namespace Lemmy.Api;

/// <summary>
/// Maps the app's enums onto the exact strings Lemmy's query parameters expect. Written as switch
/// expressions over string literals rather than <c>Enum.ToString</c> so the wire format is stated
/// once, in one file, and cannot drift when an enum member is renamed.
/// </summary>
internal static class WireValues
{
    internal static string ToWire(this ListingType listing) => listing switch
    {
        ListingType.All => "All",
        ListingType.Local => "Local",
        ListingType.Subscribed => "Subscribed",
        ListingType.ModeratorView => "ModeratorView",
        _ => "All",
    };

    internal static string ToWire(this PostSortType sort) => sort switch
    {
        PostSortType.Active => "Active",
        PostSortType.Hot => "Hot",
        PostSortType.Scaled => "Scaled",
        PostSortType.New => "New",
        PostSortType.Old => "Old",
        PostSortType.MostComments => "MostComments",
        PostSortType.NewComments => "NewComments",
        PostSortType.TopSixHour => "TopSixHour",
        PostSortType.TopDay => "TopDay",
        PostSortType.TopWeek => "TopWeek",
        PostSortType.TopMonth => "TopMonth",
        PostSortType.TopYear => "TopYear",
        PostSortType.TopAll => "TopAll",
        PostSortType.Controversial => "Controversial",
        _ => "Active",
    };

    internal static string ToWire(this CommentSortType sort) => sort switch
    {
        CommentSortType.Hot => "Hot",
        CommentSortType.Top => "Top",
        CommentSortType.New => "New",
        CommentSortType.Old => "Old",
        CommentSortType.Controversial => "Controversial",
        _ => "Hot",
    };

    internal static string ToWire(this SearchKind kind) => kind switch
    {
        SearchKind.All => "All",
        SearchKind.Posts => "Posts",
        SearchKind.Comments => "Comments",
        SearchKind.Communities => "Communities",
        SearchKind.Users => "Users",
        _ => "All",
    };

    /// <summary>A short label for a sort, for buttons and menus.</summary>
    internal static string ToDisplayName(this PostSortType sort) => sort switch
    {
        PostSortType.MostComments => "Most comments",
        PostSortType.NewComments => "New comments",
        PostSortType.TopSixHour => "Top: 6 hours",
        PostSortType.TopDay => "Top: day",
        PostSortType.TopWeek => "Top: week",
        PostSortType.TopMonth => "Top: month",
        PostSortType.TopYear => "Top: year",
        PostSortType.TopAll => "Top: all time",
        _ => sort.ToWire(),
    };
}
