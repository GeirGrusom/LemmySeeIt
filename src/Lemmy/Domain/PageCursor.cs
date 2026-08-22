namespace Lemmy.Domain;

/// <summary>
/// An opaque "next page" token, e.g. <c>P308c595</c>. Lemmy pages its listings with a cursor rather
/// than an offset, so the only correct thing to do with one is hand it straight back to the server.
/// </summary>
public readonly record struct PageCursor
{
    private const int MaxLength = 512;

    private readonly string? token;

    /// <summary>Validates a cursor token.</summary>
    /// <exception cref="DomainValidationException">The token is empty, too long, or has whitespace.</exception>
    public PageCursor(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsLegal(value.AsSpan(), out string? reason))
        {
            throw DomainValidationException.For(nameof(PageCursor), reason!);
        }

        token = value;
    }

    /// <summary>The opaque token.</summary>
    public string Value => token ?? string.Empty;

    /// <summary><see langword="false"/> for <see langword="default"/>.</summary>
    public bool IsValid => token is not null;

    /// <summary>Validates a cursor token without throwing. Blank input means "no more pages".</summary>
    public static bool TryCreate(ReadOnlySpan<char> text, out PageCursor cursor)
    {
        ReadOnlySpan<char> trimmed = text.Trim();
        if (!IsLegal(trimmed, out _))
        {
            cursor = default;
            return false;
        }

        cursor = new PageCursor(trimmed.ToString());
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    private static bool IsLegal(ReadOnlySpan<char> value, out string? reason)
    {
        if (value.IsEmpty)
        {
            reason = "the token is empty";
            return false;
        }

        if (value.Length > MaxLength)
        {
            reason = $"the token is longer than {MaxLength} characters";
            return false;
        }

        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                reason = "the token contains whitespace";
                return false;
            }
        }

        reason = null;
        return true;
    }
}
