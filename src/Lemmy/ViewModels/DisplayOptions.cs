using System.Collections.ObjectModel;
using Lemmy.Api;
using Lemmy.Domain.Models;

namespace Lemmy.ViewModels;

/// <summary>One entry in the feed's sort picker.</summary>
/// <param name="Value">The sort choosing this entry applies.</param>
/// <param name="Label">What the entry reads as.</param>
public readonly record struct SortOption(PostSortType Value, string Label);

/// <summary>One entry in a comment thread's sort picker.</summary>
/// <param name="Value">The sort choosing this entry applies.</param>
/// <param name="Label">What the entry reads as.</param>
public readonly record struct CommentSortOption(CommentSortType Value, string Label);

/// <summary>One entry in a listing picker.</summary>
/// <param name="Value">The listing choosing this entry applies.</param>
/// <param name="Label">What the entry reads as.</param>
public readonly record struct ListingOption(ListingType Value, string Label);

/// <summary>One entry in the section bar.</summary>
/// <param name="Value">The section choosing this entry goes to.</param>
/// <param name="Label">What the entry reads as.</param>
public readonly record struct SectionOption(AppSection Value, string Label);

/// <summary>
/// The fixed sets of choices the toolbars offer. Each entry pairs a value with its wording, so a
/// binding reads a label instead of calling <c>ToString</c> on an enum — which keeps the wording out
/// of the domain and reflection out of the UI. Concrete per-enum types rather than one generic
/// option type, because XAML addresses a generic type badly and these are read from XAML.
/// </summary>
public static class DisplayOptions
{
    /// <summary>The post sorts worth offering; Lemmy accepts more, but these are the ones people use.</summary>
    public static ReadOnlyCollection<SortOption> PostSorts { get; } = new(
    [
        new(PostSortType.Active, "Active"),
        new(PostSortType.Hot, "Hot"),
        new(PostSortType.Scaled, "Scaled"),
        new(PostSortType.New, "New"),
        new(PostSortType.TopDay, PostSortType.TopDay.ToDisplayName()),
        new(PostSortType.TopWeek, PostSortType.TopWeek.ToDisplayName()),
        new(PostSortType.TopMonth, PostSortType.TopMonth.ToDisplayName()),
        new(PostSortType.TopAll, PostSortType.TopAll.ToDisplayName()),
        new(PostSortType.MostComments, PostSortType.MostComments.ToDisplayName()),
    ]);

    /// <summary>The comment sorts.</summary>
    public static ReadOnlyCollection<CommentSortOption> CommentSorts { get; } = new(
    [
        new(CommentSortType.Hot, "Hot"),
        new(CommentSortType.Top, "Top"),
        new(CommentSortType.New, "New"),
        new(CommentSortType.Old, "Old"),
        new(CommentSortType.Controversial, "Controversial"),
    ]);

    /// <summary>The listings an anonymous reader can use.</summary>
    public static ReadOnlyCollection<ListingOption> Listings { get; } = new(
    [
        new(ListingType.All, "All"),
        new(ListingType.Local, "Local"),
    ]);

    /// <summary>
    /// The listings once signed in. Subscribed comes first because it is the reason most people
    /// sign in at all.
    /// </summary>
    public static ReadOnlyCollection<ListingOption> SignedInListings { get; } = new(
    [
        new(ListingType.Subscribed, "Subscribed"),
        new(ListingType.All, "All"),
        new(ListingType.Local, "Local"),
    ]);

    /// <summary>The top-level sections.</summary>
    public static ReadOnlyCollection<SectionOption> Sections { get; } = new(
    [
        new(AppSection.Feed, "Feed"),
        new(AppSection.Communities, "Communities"),
        new(AppSection.Search, "Search"),
    ]);
}
