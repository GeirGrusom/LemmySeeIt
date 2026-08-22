using System.Globalization;

namespace Lemmy.Domain;

/// <summary>
/// Identifies a post.
/// </summary>
/// <remarks>
/// Identifiers are scoped to a single instance: the same number means a different thing on a
/// different server, so pair one with an <see cref="InstanceAddress"/> before persisting it.
/// </remarks>
public readonly record struct PostId : IIdentifier<PostId>
{
    /// <summary>Wraps a raw identifier, throwing when it is not positive.</summary>
    /// <exception cref="DomainValidationException">The value is zero or negative.</exception>
    public PostId(int value) => Value = NumericIdentifier.Validate(value, nameof(PostId));

    /// <inheritdoc />
    public int Value { get; }

    /// <inheritdoc />
    public bool IsValid => NumericIdentifier.IsInRange(Value);

    /// <inheritdoc />
    public static bool TryCreate(int value, out PostId identifier)
    {
        identifier = NumericIdentifier.IsInRange(value) ? new PostId(value) : default;
        return identifier.IsValid;
    }

    /// <summary>Parses a decimal identifier, throwing when the text is not a positive integer.</summary>
    public static PostId Parse(ReadOnlySpan<char> text) => NumericIdentifier.Parse<PostId>(text, nameof(PostId));

    /// <summary>Parses a decimal identifier without throwing.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out PostId result) => NumericIdentifier.TryParse(text, out result);

    static PostId IParsable<PostId>.Parse(string text, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Parse(text.AsSpan());
    }

    static bool IParsable<PostId>.TryParse(string? text, IFormatProvider? provider, out PostId result) =>
        TryParse(text.AsSpan(), out result);

    static PostId ISpanParsable<PostId>.Parse(ReadOnlySpan<char> text, IFormatProvider? provider) => Parse(text);

    static bool ISpanParsable<PostId>.TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out PostId result) =>
        TryParse(text, out result);

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);

    /// <summary>Writes the identifier into <paramref name="destination"/> without allocating.</summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Value.TryFormat(destination, out charsWritten, format, provider);
}
