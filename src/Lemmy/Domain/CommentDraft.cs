namespace Lemmy.Domain;

/// <summary>
/// A comment body on its way to the server. Unlike <see cref="MarkdownText"/>, which models whatever
/// arrived and tolerates emptiness, this models something the reader typed and is only valid when
/// there is actually something to post.
/// </summary>
public readonly record struct CommentDraft
{
    /// <summary>The longest comment Lemmy will accept.</summary>
    public const int MaxLength = 10_000;

    private readonly string? content;

    /// <summary>Validates a body typed by the reader.</summary>
    /// <exception cref="DomainValidationException">The text is blank or over <see cref="MaxLength"/>.</exception>
    public CommentDraft(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!IsLegal(value.AsSpan(), out string? reason))
        {
            throw DomainValidationException.For(nameof(CommentDraft), reason!);
        }

        // Trailing whitespace from a text box is not part of what was written, and a comment that
        // differs from another only by it is the same comment.
        content = value.Trim();
    }

    /// <summary>The body as it will be sent.</summary>
    public string Value => content ?? string.Empty;

    /// <summary><see langword="false"/> for <see langword="default"/>.</summary>
    public bool IsValid => content is not null;

    /// <summary>Validates without throwing, for a text box that changes on every keystroke.</summary>
    public static bool TryCreate(ReadOnlySpan<char> value, out CommentDraft draft)
    {
        if (!IsLegal(value, out _))
        {
            draft = default;
            return false;
        }

        draft = new CommentDraft(value.ToString());
        return true;
    }

    /// <summary>Why <paramref name="value"/> cannot be posted, or <see langword="null"/> when it can.</summary>
    public static string? Explain(ReadOnlySpan<char> value)
    {
        _ = IsLegal(value, out string? reason);
        return reason;
    }

    private static bool IsLegal(ReadOnlySpan<char> value, out string? reason)
    {
        ReadOnlySpan<char> trimmed = value.Trim();

        if (trimmed.IsEmpty)
        {
            reason = "a comment needs something in it";
            return false;
        }

        if (trimmed.Length > MaxLength)
        {
            reason = $"a comment can be at most {MaxLength} characters, and this is {trimmed.Length}";
            return false;
        }

        reason = null;
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
