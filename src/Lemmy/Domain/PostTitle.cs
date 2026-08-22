namespace Lemmy.Domain;

/// <summary>
/// A post's headline. Lemmy caps titles at 200 characters and never allows an empty one, so a
/// <see cref="PostTitle"/> is always safe to render on a single line without a null check.
/// </summary>
public readonly record struct PostTitle
{
    /// <summary>The longest title a Lemmy server will accept.</summary>
    public const int MaxLength = 200;

    private readonly string? text;

    /// <summary>Validates a title.</summary>
    /// <exception cref="DomainValidationException">The title is blank or longer than <see cref="MaxLength"/>.</exception>
    public PostTitle(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsLegal(value.AsSpan(), out string? reason))
        {
            throw DomainValidationException.For(nameof(PostTitle), reason!);
        }

        text = value;
    }

    /// <summary>The title text.</summary>
    public string Value => text ?? string.Empty;

    /// <summary><see langword="false"/> for <see langword="default"/>.</summary>
    public bool IsValid => text is not null;

    /// <summary>
    /// Validates a title without throwing. Whitespace is collapsed first: titles arrive with stray
    /// newlines often enough that rejecting them outright would drop real posts from the feed.
    /// </summary>
    public static bool TryCreate(ReadOnlySpan<char> value, out PostTitle title)
    {
        ReadOnlySpan<char> trimmed = value.Trim();
        if (!IsLegal(trimmed, out _))
        {
            title = default;
            return false;
        }

        title = new PostTitle(CollapseWhitespace(trimmed));
        return true;
    }

    /// <summary>
    /// Builds a title from text that may be over-long, clipping it rather than rejecting it.
    /// Federated posts reach us from software with its own limits, and a clipped headline is far
    /// better than a post silently missing from the feed. Returns <see langword="null"/> for blank text.
    /// </summary>
    public static PostTitle? CreateTruncated(ReadOnlySpan<char> value)
    {
        ReadOnlySpan<char> trimmed = value.Trim();
        if (trimmed.IsEmpty)
        {
            return null;
        }

        if (trimmed.Length > MaxLength)
        {
            trimmed = trimmed[..(MaxLength - 1)];
        }

        return TryCreate(trimmed, out PostTitle title) ? title : null;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    private static bool IsLegal(ReadOnlySpan<char> value, out string? reason)
    {
        ReadOnlySpan<char> trimmed = value.Trim();

        if (trimmed.IsEmpty)
        {
            reason = "the title is blank";
            return false;
        }

        if (trimmed.Length > MaxLength)
        {
            reason = $"the title is {trimmed.Length} characters, over the {MaxLength} limit";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>Replaces every run of whitespace with a single space, writing through a stack buffer.</summary>
    private static string CollapseWhitespace(ReadOnlySpan<char> value)
    {
        Span<char> buffer = stackalloc char[MaxLength];
        int written = 0;
        bool previousWasSpace = false;

        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                previousWasSpace = true;
                continue;
            }

            if (previousWasSpace && written > 0)
            {
                buffer[written++] = ' ';
            }

            previousWasSpace = false;
            buffer[written++] = character;
        }

        return new string(buffer[..written]);
    }
}
