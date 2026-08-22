namespace Lemmy.Domain;

/// <summary>
/// The ActivityPub identity of a post, comment, community or person, e.g.
/// <c>https://lemmy.world/c/technology</c>. Unlike a <see cref="PostId"/> this is stable across
/// the whole fediverse, which makes it the right key for "is this the same thing I already saw?".
/// </summary>
public readonly record struct ActorId : ISpanParsable<ActorId>
{
    private readonly WebLink link;

    /// <summary>Validates an ActivityPub identity URL.</summary>
    /// <exception cref="DomainValidationException">The text is not an absolute HTTP(S) URL.</exception>
    public ActorId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        link = WebLink.TryParse(value.AsSpan(), out WebLink parsed)
            ? parsed
            : throw DomainValidationException.For(nameof(ActorId), $"'{value}' is not an absolute http(s) URL");
    }

    /// <summary>The identity URL as text.</summary>
    public string Value => link.Value;

    /// <summary><see langword="false"/> for <see langword="default"/>.</summary>
    public bool IsValid => link.IsValid;

    /// <summary>The link form, for opening the item in a browser.</summary>
    public WebLink Link => link;

    /// <summary>The instance that owns the item, or <see langword="null"/> when the host is unusable.</summary>
    public InstanceAddress? Instance =>
        InstanceAddress.TryCreate(link.Host, out InstanceAddress address) ? address : null;

    /// <summary>Validates an ActivityPub identity URL without throwing.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out ActorId result)
    {
        if (!WebLink.TryParse(text, out WebLink parsed))
        {
            result = default;
            return false;
        }

        result = new ActorId(parsed.Value);
        return true;
    }

    /// <summary>Validates an ActivityPub identity URL, throwing on rejection.</summary>
    /// <exception cref="DomainValidationException">The text is not an absolute HTTP(S) URL.</exception>
    public static ActorId Parse(ReadOnlySpan<char> text) =>
        TryParse(text, out ActorId result)
            ? result
            : throw DomainValidationException.For(nameof(ActorId), $"'{text.ToString()}' is not an absolute http(s) URL");

    static ActorId IParsable<ActorId>.Parse(string text, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Parse(text.AsSpan());
    }

    static bool IParsable<ActorId>.TryParse(string? text, IFormatProvider? provider, out ActorId result) =>
        TryParse(text.AsSpan(), out result);

    static ActorId ISpanParsable<ActorId>.Parse(ReadOnlySpan<char> text, IFormatProvider? provider) => Parse(text);

    static bool ISpanParsable<ActorId>.TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out ActorId result) =>
        TryParse(text, out result);

    /// <inheritdoc />
    public override string ToString() => Value;
}
