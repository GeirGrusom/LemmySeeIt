namespace Lemmy.Api.Dto;

/// <summary>A community exactly as Lemmy's API v3 sends it.</summary>
internal sealed record CommunityWire
{
    public int Id { get; init; }

    public string? Name { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? Icon { get; init; }

    public string? Banner { get; init; }

    public string? ActorId { get; init; }

    public bool Local { get; init; }

    public bool Nsfw { get; init; }

    public bool Removed { get; init; }

    public bool Deleted { get; init; }

    public bool Hidden { get; init; }

    public bool PostingRestrictedToMods { get; init; }

    public int InstanceId { get; init; }

    public DateTimeOffset Published { get; init; }

    public DateTimeOffset? Updated { get; init; }
}
