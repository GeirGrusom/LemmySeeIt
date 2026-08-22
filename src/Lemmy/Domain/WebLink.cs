namespace Lemmy.Domain;

/// <summary>
/// An absolute <c>http</c> or <c>https</c> link that came off the wire — a post's target URL, a
/// thumbnail, an avatar. The text is kept rather than a <see cref="Uri"/> so links stay cheap to
/// compare and to use as cache keys; call <see cref="ToUri"/> when a <see cref="Uri"/> is needed.
/// </summary>
public readonly record struct WebLink : ISpanParsable<WebLink>
{
    private readonly string? absoluteUri;

    /// <summary>Validates an absolute HTTP(S) link.</summary>
    /// <exception cref="DomainValidationException">The text is not an absolute HTTP(S) URL.</exception>
    public WebLink(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsLegal(value, out string? reason))
        {
            throw DomainValidationException.For(nameof(WebLink), reason!);
        }

        absoluteUri = value;
    }

    /// <summary>The absolute URL as text.</summary>
    public string Value => absoluteUri ?? string.Empty;

    /// <summary><see langword="false"/> for <see langword="default"/>.</summary>
    public bool IsValid => absoluteUri is not null;

    /// <summary>The host portion, handy for the "via example.com" line on a link post.</summary>
    public ReadOnlySpan<char> Host
    {
        get
        {
            ReadOnlySpan<char> span = Value.AsSpan();
            int schemeIndex = span.IndexOf("://", StringComparison.Ordinal);
            if (schemeIndex < 0)
            {
                return ReadOnlySpan<char>.Empty;
            }

            ReadOnlySpan<char> rest = span[(schemeIndex + 3)..];
            int pathIndex = rest.IndexOfAny('/', '?', '#');
            ReadOnlySpan<char> host = pathIndex < 0 ? rest : rest[..pathIndex];

            return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
        }
    }

    /// <summary>
    /// Whether the link's extension suggests an image. A hint for layout only — the server's
    /// reported content type is authoritative when we have one.
    /// </summary>
    public bool LooksLikeImage
    {
        get
        {
            ReadOnlySpan<char> span = Value.AsSpan();
            int queryIndex = span.IndexOfAny('?', '#');
            if (queryIndex >= 0)
            {
                span = span[..queryIndex];
            }

            int dotIndex = span.LastIndexOf('.');
            if (dotIndex < 0)
            {
                return false;
            }

            ReadOnlySpan<char> extension = span[(dotIndex + 1)..];
            return extension.Equals("jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals("jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals("png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals("gif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals("webp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals("avif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals("bmp", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Materialises a <see cref="Uri"/> for the platform APIs that need one.</summary>
    /// <exception cref="InvalidOperationException">The link is <see langword="default"/>.</exception>
    public Uri ToUri() =>
        absoluteUri is null
            ? throw new InvalidOperationException("A default WebLink has no URL.")
            : new Uri(absoluteUri, UriKind.Absolute);

    /// <summary>Validates an absolute HTTP(S) link without throwing.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out WebLink result)
    {
        ReadOnlySpan<char> trimmed = text.Trim();
        if (trimmed.IsEmpty)
        {
            result = default;
            return false;
        }

        string candidate = trimmed.ToString();
        if (!IsLegal(candidate, out _))
        {
            result = default;
            return false;
        }

        result = new WebLink(candidate);
        return true;
    }

    /// <summary>Validates an absolute HTTP(S) link, throwing on rejection.</summary>
    /// <exception cref="DomainValidationException">The text is not an absolute HTTP(S) URL.</exception>
    public static WebLink Parse(ReadOnlySpan<char> text) =>
        TryParse(text, out WebLink result)
            ? result
            : throw DomainValidationException.For(nameof(WebLink), $"'{text.ToString()}' is not an absolute http(s) URL");

    static WebLink IParsable<WebLink>.Parse(string text, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Parse(text.AsSpan());
    }

    static bool IParsable<WebLink>.TryParse(string? text, IFormatProvider? provider, out WebLink result) =>
        TryParse(text.AsSpan(), out result);

    static WebLink ISpanParsable<WebLink>.Parse(ReadOnlySpan<char> text, IFormatProvider? provider) => Parse(text);

    static bool ISpanParsable<WebLink>.TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out WebLink result) =>
        TryParse(text, out result);

    /// <inheritdoc />
    public override string ToString() => Value;

    private static bool IsLegal(string value, out string? reason)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed))
        {
            reason = $"'{value}' is not an absolute URL";
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            reason = $"'{parsed.Scheme}' is not an http(s) scheme";
            return false;
        }

        reason = null;
        return true;
    }
}
