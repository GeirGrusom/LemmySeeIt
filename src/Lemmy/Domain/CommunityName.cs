namespace Lemmy.Domain;

/// <summary>
/// The short, instance-local name of a community — the <c>technology</c> in
/// <c>!technology@lemmy.world</c>. Lemmy restricts these to lower-case ASCII letters, digits and
/// underscores, which is exactly what makes them safe to drop into a URL unescaped.
/// </summary>
public readonly record struct CommunityName : ISpanParsable<CommunityName>
{
    private const int MinLength = 1;
    private const int MaxLength = 60;

    private readonly string? name;

    /// <summary>Validates a community name.</summary>
    /// <exception cref="DomainValidationException">The name is not a legal community name.</exception>
    public CommunityName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsLegal(value.AsSpan(), out string? reason))
        {
            throw DomainValidationException.For(nameof(CommunityName), reason!);
        }

        name = value;
    }

    /// <summary>The bare community name, without the leading <c>!</c> or the instance suffix.</summary>
    public string Value => name ?? string.Empty;

    /// <summary><see langword="false"/> for <see langword="default"/>.</summary>
    public bool IsValid => name is not null;

    /// <summary>Validates a community name without throwing.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out CommunityName result)
    {
        ReadOnlySpan<char> trimmed = text.Trim();
        if (!trimmed.IsEmpty && trimmed[0] == '!')
        {
            trimmed = trimmed[1..];
        }

        int atIndex = trimmed.IndexOf('@');
        if (atIndex >= 0)
        {
            trimmed = trimmed[..atIndex];
        }

        if (!IsLegal(trimmed, out _))
        {
            result = default;
            return false;
        }

        result = new CommunityName(trimmed.ToString());
        return true;
    }

    /// <summary>Validates a community name, throwing on rejection.</summary>
    /// <exception cref="DomainValidationException">The name is not a legal community name.</exception>
    public static CommunityName Parse(ReadOnlySpan<char> text) =>
        TryParse(text, out CommunityName result)
            ? result
            : throw DomainValidationException.For(nameof(CommunityName), $"'{text.ToString()}' is not a legal community name");

    static CommunityName IParsable<CommunityName>.Parse(string text, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Parse(text.AsSpan());
    }

    static bool IParsable<CommunityName>.TryParse(string? text, IFormatProvider? provider, out CommunityName result) =>
        TryParse(text.AsSpan(), out result);

    static CommunityName ISpanParsable<CommunityName>.Parse(ReadOnlySpan<char> text, IFormatProvider? provider) => Parse(text);

    static bool ISpanParsable<CommunityName>.TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out CommunityName result) =>
        TryParse(text, out result);

    /// <inheritdoc />
    public override string ToString() => Value;

    private static bool IsLegal(ReadOnlySpan<char> value, out string? reason)
    {
        if (value.Length is < MinLength or > MaxLength)
        {
            reason = $"the name must be {MinLength}-{MaxLength} characters but was {value.Length}";
            return false;
        }

        foreach (char character in value)
        {
            if (!char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character) && character != '_')
            {
                reason = $"'{value.ToString()}' contains '{character}'; only a-z, 0-9 and _ are allowed";
                return false;
            }
        }

        reason = null;
        return true;
    }
}
