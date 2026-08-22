using System.Globalization;

namespace Lemmy.Domain;

/// <summary>
/// Identifies a person — a user account, local or federated.
/// </summary>
/// <remarks>
/// Identifiers are scoped to a single instance: the same number means a different thing on a
/// different server, so pair one with an <see cref="InstanceAddress"/> before persisting it.
/// </remarks>
public readonly record struct PersonId : IIdentifier<PersonId>
{
    /// <summary>Wraps a raw identifier, throwing when it is not positive.</summary>
    /// <exception cref="DomainValidationException">The value is zero or negative.</exception>
    public PersonId(int value) => Value = NumericIdentifier.Validate(value, nameof(PersonId));

    /// <inheritdoc />
    public int Value { get; }

    /// <inheritdoc />
    public bool IsValid => NumericIdentifier.IsInRange(Value);

    /// <inheritdoc />
    public static bool TryCreate(int value, out PersonId identifier)
    {
        identifier = NumericIdentifier.IsInRange(value) ? new PersonId(value) : default;
        return identifier.IsValid;
    }

    /// <summary>Parses a decimal identifier, throwing when the text is not a positive integer.</summary>
    public static PersonId Parse(ReadOnlySpan<char> text) => NumericIdentifier.Parse<PersonId>(text, nameof(PersonId));

    /// <summary>Parses a decimal identifier without throwing.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out PersonId result) => NumericIdentifier.TryParse(text, out result);

    static PersonId IParsable<PersonId>.Parse(string text, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Parse(text.AsSpan());
    }

    static bool IParsable<PersonId>.TryParse(string? text, IFormatProvider? provider, out PersonId result) =>
        TryParse(text.AsSpan(), out result);

    static PersonId ISpanParsable<PersonId>.Parse(ReadOnlySpan<char> text, IFormatProvider? provider) => Parse(text);

    static bool ISpanParsable<PersonId>.TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out PersonId result) =>
        TryParse(text, out result);

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);

    /// <summary>Writes the identifier into <paramref name="destination"/> without allocating.</summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Value.TryFormat(destination, out charsWritten, format, provider);
}
