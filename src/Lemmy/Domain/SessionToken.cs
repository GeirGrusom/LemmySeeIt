namespace Lemmy.Domain;

/// <summary>
/// The bearer token a Lemmy instance issues at sign-in. Treated as opaque: it is a JWT today, but
/// nothing here reads it, and Lemmy tokens carry no expiry — so the only way to end a session is to
/// tell the server, not to wait.
/// </summary>
/// <remarks>
/// Deliberately has no <c>ToString</c> override returning the token. A secret that prints itself
/// ends up in a log eventually.
/// </remarks>
public readonly record struct SessionToken
{
    private const int MaxLength = 4096;

    private readonly string? token;

    /// <summary>Wraps a token issued by an instance.</summary>
    /// <exception cref="DomainValidationException">The text is empty, over-long, or has whitespace.</exception>
    public SessionToken(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsLegal(value.AsSpan(), out string? reason))
        {
            throw DomainValidationException.For(nameof(SessionToken), reason!);
        }

        token = value;
    }

    /// <summary>The token, for putting on a request. Nothing else should need it.</summary>
    public string Value => token ?? string.Empty;

    /// <summary><see langword="false"/> for <see langword="default"/>, which is "signed out".</summary>
    public bool IsValid => token is not null;

    /// <summary>Wraps a token without throwing.</summary>
    public static bool TryCreate(ReadOnlySpan<char> value, out SessionToken sessionToken)
    {
        ReadOnlySpan<char> trimmed = value.Trim();
        if (!IsLegal(trimmed, out _))
        {
            sessionToken = default;
            return false;
        }

        sessionToken = new SessionToken(trimmed.ToString());
        return true;
    }

    /// <summary>Says nothing. The token is a secret and this type refuses to help leak it.</summary>
    public override string ToString() => IsValid ? "SessionToken(set)" : "SessionToken(none)";

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
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                reason = "the token contains whitespace or control characters";
                return false;
            }
        }

        reason = null;
        return true;
    }
}
