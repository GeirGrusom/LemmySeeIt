namespace Lemmy.Domain;

/// <summary>
/// The host of a Lemmy server, such as <c>lemmy.world</c>. Stored as a bare host — no scheme, no
/// port-less trailing slash, no path — because that is the form Lemmy itself uses inside
/// fully-qualified names like <c>!technology@lemmy.world</c>.
/// </summary>
public readonly record struct InstanceAddress : ISpanParsable<InstanceAddress>
{
    private const int MaxHostLength = 253;

    private readonly string? host;

    /// <summary>Validates and normalises a host name.</summary>
    /// <exception cref="DomainValidationException">The text is not a usable host name.</exception>
    public InstanceAddress(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        host = Normalise(value.AsSpan(), out string? reason)
            ?? throw DomainValidationException.For(nameof(InstanceAddress), reason!);
    }

    private InstanceAddress(string normalisedHost, bool _) => host = normalisedHost;

    /// <summary>The normalised, lower-case host name.</summary>
    public string Value => host ?? string.Empty;

    /// <summary><see langword="false"/> for <see langword="default"/>.</summary>
    public bool IsValid => host is not null;

    /// <summary>The HTTPS root the API client hangs its requests off, e.g. <c>https://lemmy.world/</c>.</summary>
    public Uri BaseUri => new($"https://{Value}/", UriKind.Absolute);

    /// <summary>Validates and normalises a host name without throwing.</summary>
    public static bool TryCreate(ReadOnlySpan<char> value, out InstanceAddress address)
    {
        string? normalised = Normalise(value, out _);
        address = normalised is null ? default : new InstanceAddress(normalised, true);
        return normalised is not null;
    }

    /// <summary>
    /// Accepts what a user is likely to type or paste — <c>lemmy.world</c>,
    /// <c>https://lemmy.world</c>, or <c>https://lemmy.world/c/technology</c> — and keeps the host.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> text, out InstanceAddress address)
    {
        ReadOnlySpan<char> trimmed = text.Trim();

        int schemeIndex = trimmed.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0)
        {
            trimmed = trimmed[(schemeIndex + 3)..];
        }

        int pathIndex = trimmed.IndexOfAny('/', '?', '#');
        if (pathIndex >= 0)
        {
            trimmed = trimmed[..pathIndex];
        }

        int atIndex = trimmed.LastIndexOf('@');
        if (atIndex >= 0)
        {
            trimmed = trimmed[(atIndex + 1)..];
        }

        return TryCreate(trimmed, out address);
    }

    /// <summary>Parses a host name, throwing when the text does not contain one.</summary>
    /// <exception cref="DomainValidationException">No usable host could be read from the text.</exception>
    public static InstanceAddress Parse(ReadOnlySpan<char> text) =>
        TryParse(text, out InstanceAddress address)
            ? address
            : throw DomainValidationException.For(nameof(InstanceAddress), $"'{text.ToString()}' does not contain a host name");

    static InstanceAddress IParsable<InstanceAddress>.Parse(string text, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Parse(text.AsSpan());
    }

    static bool IParsable<InstanceAddress>.TryParse(string? text, IFormatProvider? provider, out InstanceAddress result) =>
        TryParse(text.AsSpan(), out result);

    static InstanceAddress ISpanParsable<InstanceAddress>.Parse(ReadOnlySpan<char> text, IFormatProvider? provider) => Parse(text);

    static bool ISpanParsable<InstanceAddress>.TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out InstanceAddress result) =>
        TryParse(text, out result);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Checks the host and lower-cases it, returning <see langword="null"/> plus a reason on failure.
    /// A host must have at least one dot: a single label cannot be a real federated instance.
    /// </summary>
    private static string? Normalise(ReadOnlySpan<char> value, out string? reason)
    {
        ReadOnlySpan<char> trimmed = value.Trim();

        if (trimmed.IsEmpty)
        {
            reason = "the host is empty";
            return null;
        }

        if (trimmed.Length > MaxHostLength)
        {
            reason = $"the host is longer than {MaxHostLength} characters";
            return null;
        }

        if (trimmed.IndexOf('.') < 0)
        {
            reason = $"'{trimmed.ToString()}' has no dot, so it cannot be a public host";
            return null;
        }

        if (trimmed[0] is '.' or '-' || trimmed[^1] is '.' or '-')
        {
            reason = $"'{trimmed.ToString()}' starts or ends with a separator";
            return null;
        }

        foreach (char character in trimmed)
        {
            bool allowed = char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or ':';
            if (!allowed)
            {
                reason = $"'{trimmed.ToString()}' contains '{character}', which is not valid in a host";
                return null;
            }
        }

        if (trimmed.Contains("..", StringComparison.Ordinal))
        {
            reason = $"'{trimmed.ToString()}' contains an empty label";
            return null;
        }

        Span<char> buffer = trimmed.Length <= 256 ? stackalloc char[256] : new char[trimmed.Length];
        int written = trimmed.ToLowerInvariant(buffer);

        reason = null;
        return new string(buffer[..written]);
    }
}
