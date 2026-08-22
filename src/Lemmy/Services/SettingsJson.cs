using System.Text.Json.Serialization;

namespace Lemmy.Services;

/// <summary>
/// The shape settings take on disk. Deliberately primitive: a stored file outlives any given build,
/// so it holds plain strings that the loader re-validates rather than domain types that a future
/// rename would silently invalidate.
/// </summary>
internal sealed record SettingsDocument
{
    public string? Instance { get; init; }

    public string? Listing { get; init; }

    public string? Sort { get; init; }

    public string? CommentSort { get; init; }

    public bool ShowNsfw { get; init; }

    public bool BlurNsfwImages { get; init; } = true;
}

/// <summary>Source-generated serializer for the settings file.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SettingsDocument))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
