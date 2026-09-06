using System.Globalization;

namespace Lemmy.Domain;

/// <summary>
/// Identifies a notification — a reply row or a mention row. The two are numbered separately by
/// the server, so a value only means something alongside the <see cref="Lemmy.Domain.Models.NotificationKind"/> it came with.
/// </summary>
/// <remarks>
/// Identifiers are scoped to a single instance: the same number means a different thing on a
/// different server, so pair one with an <see cref="InstanceAddress"/> before persisting it.
/// </remarks>
public readonly record struct NotificationId : IIdentifier<NotificationId>
{
    /// <summary>Wraps a raw identifier, throwing when it is not positive.</summary>
    /// <exception cref="DomainValidationException">The value is zero or negative.</exception>
    public NotificationId(int value) => Value = NumericIdentifier.Validate(value, nameof(NotificationId));

    /// <inheritdoc />
    public int Value { get; }

    /// <inheritdoc />
    public bool IsValid => NumericIdentifier.IsInRange(Value);

    /// <inheritdoc />
    public static bool TryCreate(int value, out NotificationId identifier)
    {
        identifier = NumericIdentifier.IsInRange(value) ? new NotificationId(value) : default;
        return identifier.IsValid;
    }

    /// <summary>Parses a decimal identifier, throwing when the text is not a positive integer.</summary>
    public static NotificationId Parse(ReadOnlySpan<char> text) => NumericIdentifier.Parse<NotificationId>(text, nameof(NotificationId));

    /// <summary>Parses a decimal identifier without throwing.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out NotificationId result) => NumericIdentifier.TryParse(text, out result);

    static NotificationId IParsable<NotificationId>.Parse(string text, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Parse(text.AsSpan());
    }

    static bool IParsable<NotificationId>.TryParse(string? text, IFormatProvider? provider, out NotificationId result) =>
        TryParse(text.AsSpan(), out result);

    static NotificationId ISpanParsable<NotificationId>.Parse(ReadOnlySpan<char> text, IFormatProvider? provider) => Parse(text);

    static bool ISpanParsable<NotificationId>.TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out NotificationId result) =>
        TryParse(text, out result);

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);

    /// <summary>Writes the identifier into <paramref name="destination"/> without allocating.</summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Value.TryFormat(destination, out charsWritten, format, provider);
}
