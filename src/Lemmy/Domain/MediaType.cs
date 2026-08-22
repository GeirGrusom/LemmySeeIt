namespace Lemmy.Domain;

/// <summary>
/// An IANA media type as the server reported it for a link, e.g. <c>image/jpeg</c>. This is the
/// authoritative answer to "is this post an image": plenty of image links carry no file extension
/// at all — an instance's own <c>image_proxy</c> endpoint is one — so sniffing the URL is a fallback,
/// not a substitute.
/// </summary>
public readonly record struct MediaType
{
    private const int MaxLength = 255;

    private readonly string? value;

    /// <summary>Validates and normalises a media type.</summary>
    /// <exception cref="DomainValidationException">The text is not a <c>type/subtype</c> pair.</exception>
    public MediaType(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        value = Normalise(text.AsSpan(), out string? reason)
            ?? throw DomainValidationException.For(nameof(MediaType), reason!);
    }

    private MediaType(string normalised, bool _) => value = normalised;

    /// <summary>The normalised media type, lower-cased and without any parameters.</summary>
    public string Value => value ?? string.Empty;

    /// <summary><see langword="false"/> for <see langword="default"/>.</summary>
    public bool IsValid => value is not null;

    /// <summary>Whether the link points at an image.</summary>
    public bool IsImage => Value.StartsWith("image/", StringComparison.Ordinal);

    /// <summary>Whether the image is one that animates, which is worth knowing before scaling it.</summary>
    public bool IsAnimatedImage => Value is "image/gif" or "image/webp" or "image/apng";

    /// <summary>The part before the slash, e.g. <c>image</c>.</summary>
    public ReadOnlySpan<char> Kind
    {
        get
        {
            ReadOnlySpan<char> span = Value.AsSpan();
            int slash = span.IndexOf('/');
            return slash < 0 ? ReadOnlySpan<char>.Empty : span[..slash];
        }
    }

    /// <summary>Validates a media type without throwing.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out MediaType mediaType)
    {
        string? normalised = Normalise(text, out _);
        mediaType = normalised is null ? default : new MediaType(normalised, true);
        return normalised is not null;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Checks the shape and lower-cases it, dropping any parameters: servers routinely answer
    /// <c>text/html; charset=utf-8</c>, and the charset says nothing about what the link is.
    /// </summary>
    private static string? Normalise(ReadOnlySpan<char> text, out string? reason)
    {
        ReadOnlySpan<char> trimmed = text.Trim();

        int semicolon = trimmed.IndexOf(';');
        if (semicolon >= 0)
        {
            trimmed = trimmed[..semicolon].TrimEnd();
        }

        if (trimmed.IsEmpty)
        {
            reason = "the media type is empty";
            return null;
        }

        if (trimmed.Length > MaxLength)
        {
            reason = $"the media type is longer than {MaxLength} characters";
            return null;
        }

        int slash = trimmed.IndexOf('/');
        if (slash <= 0 || slash == trimmed.Length - 1)
        {
            reason = $"'{trimmed.ToString()}' is not a type/subtype pair";
            return null;
        }

        if (trimmed[(slash + 1)..].IndexOf('/') >= 0)
        {
            reason = $"'{trimmed.ToString()}' has more than one slash";
            return null;
        }

        foreach (char character in trimmed)
        {
            bool allowed = char.IsAsciiLetterOrDigit(character) || character is '/' or '.' or '+' or '-' or '_';
            if (!allowed)
            {
                reason = $"'{trimmed.ToString()}' contains '{character}', which is not valid in a media type";
                return null;
            }
        }

        Span<char> buffer = trimmed.Length <= 256 ? stackalloc char[256] : new char[trimmed.Length];
        int written = trimmed.ToLowerInvariant(buffer);

        reason = null;
        return new string(buffer[..written]);
    }
}
