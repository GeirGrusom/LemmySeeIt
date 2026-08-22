using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lemmy.Api;

/// <summary>
/// Reads Lemmy timestamps, which are UTC but are not always labelled as such: current builds send
/// <c>2026-08-20T20:31:39.106789Z</c> while older ones drop the trailing <c>Z</c>. The default
/// reader treats an unlabelled timestamp as local time, which would shift every "3 hours ago" by
/// the reader's own offset — worst for the people furthest from UTC. This one assumes UTC instead.
/// </summary>
internal sealed class LenientTimestampConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a timestamp string but found {reader.TokenType}.");
        }

        string? text = reader.GetString();
        if (text is null)
        {
            throw new JsonException("Expected a timestamp string but found null.");
        }

        if (HasOffset(text))
        {
            return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset labelled)
                ? labelled
                : throw new JsonException($"'{text}' is not a timestamp this client understands.");
        }

        return DateTime.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTime unlabelled)
            ? new DateTimeOffset(unlabelled, TimeSpan.Zero)
            : throw new JsonException($"'{text}' is not a timestamp this client understands.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToUniversalTime());
    }

    /// <summary>
    /// Whether the text states its own offset. The date part is full of hyphens, so a sign only
    /// counts as an offset when it appears after the <c>T</c> that starts the time.
    /// </summary>
    private static bool HasOffset(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return false;
        }

        if (text[^1] is 'Z' or 'z')
        {
            return true;
        }

        int timeIndex = text.IndexOf('T');
        return timeIndex >= 0 && text[(timeIndex + 1)..].IndexOfAny('+', '-') >= 0;
    }
}
