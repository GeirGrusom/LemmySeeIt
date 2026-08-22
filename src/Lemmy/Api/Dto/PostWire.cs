using System.Text.Json.Serialization;

namespace Lemmy.Api.Dto;

/// <summary>
/// A post exactly as Lemmy's API v3 sends it. Wire types stay separate from the domain models so
/// that a server sending a null where the docs promise a string breaks one mapper, not the app.
/// </summary>
internal sealed record PostWire
{
    public int Id { get; init; }

    /// <summary>Lemmy calls the title "name"; the domain model does not.</summary>
    public string? Name { get; init; }

    public string? Body { get; init; }

    public string? Url { get; init; }

    public string? ThumbnailUrl { get; init; }

    /// <summary>What the server found at <see cref="Url"/>, e.g. <c>image/jpeg</c>.</summary>
    public string? UrlContentType { get; init; }

    public string? EmbedTitle { get; init; }

    public string? EmbedDescription { get; init; }

    public int CreatorId { get; init; }

    public int CommunityId { get; init; }

    public string? ApId { get; init; }

    public int LanguageId { get; init; }

    public bool Nsfw { get; init; }

    public bool Locked { get; init; }

    public bool Removed { get; init; }

    public bool Deleted { get; init; }

    public bool FeaturedCommunity { get; init; }

    public bool FeaturedLocal { get; init; }

    public DateTimeOffset Published { get; init; }

    public DateTimeOffset? Updated { get; init; }
}
