using System.Collections.Immutable;

namespace Lemmy.Domain.Models;

/// <summary>One page of a feed, plus the cursor that fetches the next one.</summary>
/// <param name="Posts">The posts on this page, in server order.</param>
/// <param name="NextCursor">The cursor for the following page, or <see langword="null"/> at the end of the feed.</param>
public sealed record PostPage(ImmutableArray<PostSummary> Posts, PageCursor? NextCursor)
{
    /// <summary>An empty final page.</summary>
    public static PostPage Empty { get; } = new([], null);

    /// <summary>Whether asking for another page is worth doing.</summary>
    public bool HasMore => NextCursor.HasValue;
}
