namespace Lemmy.Api.Dto;

/// <summary>An account exactly as Lemmy's API v3 sends it.</summary>
internal sealed record PersonWire
{
    public int Id { get; init; }

    public string? Name { get; init; }

    public string? DisplayName { get; init; }

    public string? Avatar { get; init; }

    public string? Banner { get; init; }

    public string? ActorId { get; init; }

    public bool Local { get; init; }

    public bool Banned { get; init; }

    public bool Deleted { get; init; }

    public bool BotAccount { get; init; }

    public int InstanceId { get; init; }

    public DateTimeOffset Published { get; init; }
}
