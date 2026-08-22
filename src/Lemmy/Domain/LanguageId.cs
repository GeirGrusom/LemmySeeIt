using System.Globalization;

namespace Lemmy.Domain;

/// <summary>
/// Identifies the language a post or comment was written in. Unlike the other identifiers this one
/// is allowed to be zero: Lemmy reserves zero for "undetermined", which is a real, common answer.
/// </summary>
public readonly record struct LanguageId : ISpanFormattable
{
    /// <summary>The language Lemmy assigns when nobody has said what the language is.</summary>
    public static LanguageId Undetermined => default;

    /// <summary>Wraps a raw language identifier.</summary>
    /// <exception cref="DomainValidationException">The value is negative.</exception>
    public LanguageId(int value)
    {
        if (value < 0)
        {
            throw DomainValidationException.For(
                nameof(LanguageId),
                $"language identifiers cannot be negative but was {value.ToString(CultureInfo.InvariantCulture)}");
        }

        Value = value;
    }

    /// <summary>The underlying language number.</summary>
    public int Value { get; }

    /// <summary><see langword="true"/> when the language is the reserved "undetermined" value.</summary>
    public bool IsUndetermined => Value == 0;

    /// <summary>Wraps a raw language identifier without throwing.</summary>
    public static bool TryCreate(int value, out LanguageId languageId)
    {
        if (value < 0)
        {
            languageId = default;
            return false;
        }

        languageId = new LanguageId(value);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);

    /// <summary>Writes the language identifier into <paramref name="destination"/> without allocating.</summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Value.TryFormat(destination, out charsWritten, format, provider);
}
