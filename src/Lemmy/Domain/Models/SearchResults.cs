using System.Collections.Immutable;

namespace Lemmy.Domain.Models;

/// <summary>What a search turned up, grouped by kind.</summary>
/// <param name="Posts">Matching posts.</param>
/// <param name="Communities">Matching communities.</param>
/// <param name="People">Matching accounts.</param>
public sealed record SearchResults(
    ImmutableArray<PostSummary> Posts,
    ImmutableArray<CommunitySummary> Communities,
    ImmutableArray<Person> People)
{
    /// <summary>A search that found nothing.</summary>
    public static SearchResults Empty { get; } = new([], [], []);

    /// <summary>Whether anything at all matched.</summary>
    public bool IsEmpty => Posts.IsEmpty && Communities.IsEmpty && People.IsEmpty;
}
