namespace Lemmy.Domain;

/// <summary>
/// The instance-local part of an account name — the <c>alice</c> in <c>@alice@lemmy.world</c>.
/// Federated accounts reach us from servers with looser rules than our own, so this type checks
/// only what matters for display and URL safety rather than mirroring Lemmy's signup rules.
/// </summary>
public readonly record struct Username : ISpanParsable<Username>
{
    private const int MaxLength = 60;

    private readonly string? name;

    /// <summary>Validates a username.</summary>
    /// <exception cref="DomainValidationException">The name is empty, too long, or contains a separator.</exception>
    public Username(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsLegal(value.AsSpan(), out string? reason))
        {
            throw DomainValidationException.For(nameof(Username), reason!);
        }

        name = value;
    }

    /// <summary>The bare username, without the leading <c>@</c> or the instance suffix.</summary>
    public string Value => name ?? string.Empty;

    /// <summary><see langword="false"/> for <see langword="default"/>.</summary>
    public bool IsValid => name is not null;

    /// <summary>Validates a username without throwing, tolerating a leading <c>@</c> and an instance suffix.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out Username result)
    {
        ReadOnlySpan<char> trimmed = text.Trim();
        if (!trimmed.IsEmpty && trimmed[0] == '@')
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

        result = new Username(trimmed.ToString());
        return true;
    }

    /// <summary>Validates a username, throwing on rejection.</summary>
    /// <exception cref="DomainValidationException">The name is not a legal username.</exception>
    public static Username Parse(ReadOnlySpan<char> text) =>
        TryParse(text, out Username result)
            ? result
            : throw DomainValidationException.For(nameof(Username), $"'{text.ToString()}' is not a legal username");

    static Username IParsable<Username>.Parse(string text, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Parse(text.AsSpan());
    }

    static bool IParsable<Username>.TryParse(string? text, IFormatProvider? provider, out Username result) =>
        TryParse(text.AsSpan(), out result);

    static Username ISpanParsable<Username>.Parse(ReadOnlySpan<char> text, IFormatProvider? provider) => Parse(text);

    static bool ISpanParsable<Username>.TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out Username result) =>
        TryParse(text, out result);

    /// <inheritdoc />
    public override string ToString() => Value;

    private static bool IsLegal(ReadOnlySpan<char> value, out string? reason)
    {
        if (value.IsEmpty)
        {
            reason = "the name is empty";
            return false;
        }

        if (value.Length > MaxLength)
        {
            reason = $"the name is longer than {MaxLength} characters";
            return false;
        }

        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character) || character is '/' or '\\' or '@' or '?' or '#')
            {
                reason = $"'{value.ToString()}' contains '{character}', which cannot appear in a username";
                return false;
            }
        }

        reason = null;
        return true;
    }
}
