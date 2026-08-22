namespace Lemmy.Api.Dto;

/// <summary>A comment exactly as Lemmy's API v3 sends it.</summary>
internal sealed record CommentWire
{
    public int Id { get; init; }

    public int PostId { get; init; }

    public int CreatorId { get; init; }

    public string? Content { get; init; }

    /// <summary>The materialised ancestor path, e.g. <c>0.25414623</c>. The only nesting signal we get.</summary>
    public string? Path { get; init; }

    public string? ApId { get; init; }

    public int LanguageId { get; init; }

    public bool Removed { get; init; }

    public bool Deleted { get; init; }

    public bool Distinguished { get; init; }

    public bool Local { get; init; }

    public DateTimeOffset Published { get; init; }

    public DateTimeOffset? Updated { get; init; }
}
