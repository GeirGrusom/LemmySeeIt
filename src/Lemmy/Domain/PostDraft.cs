namespace Lemmy.Domain;

/// <summary>
/// A post on its way to the server: what it says, not where it goes. The community is deliberately
/// not part of it — Lemmy cannot move a post between communities, so a new post needs one and an
/// edit can never use one, and a field only half the callers may set is worse than a parameter.
/// </summary>
public readonly record struct PostDraft
{
    /// <summary>The longest body a Lemmy server will accept on a post.</summary>
    public const int MaxBodyLength = 10_000;

    /// <summary>Builds a draft from parts that have already been validated.</summary>
    /// <exception cref="DomainValidationException">The title is missing, or the body is over <see cref="MaxBodyLength"/>.</exception>
    public PostDraft(PostTitle title, WebLink? url, MarkdownText body, bool isNsfw = false)
    {
        if (!title.IsValid)
        {
            throw DomainValidationException.For(nameof(PostDraft), "a post needs a title");
        }

        if (body.Value.Length > MaxBodyLength)
        {
            throw DomainValidationException.For(
                nameof(PostDraft),
                $"a post body can be at most {MaxBodyLength} characters, and this is {body.Value.Length}");
        }

        Title = title;
        Url = url;
        Body = body;
        IsNsfw = isNsfw;
    }

    /// <summary>The headline.</summary>
    public PostTitle Title { get; }

    /// <summary>What the post links to, or <see langword="null"/> for a post that only says something.</summary>
    public WebLink? Url { get; }

    /// <summary>The Markdown body, which may be empty.</summary>
    public MarkdownText Body { get; }

    /// <summary>Whether it should be flagged as not safe for work.</summary>
    public bool IsNsfw { get; }

    /// <summary><see langword="false"/> for <see langword="default"/>.</summary>
    public bool IsValid => Title.IsValid;

    /// <summary>Whether the post links somewhere.</summary>
    public bool HasUrl => Url.HasValue;

    /// <summary>Whether the post has anything written in its body.</summary>
    public bool HasBody => !Body.IsEmpty;

    /// <summary>
    /// Validates what is in the three text boxes without throwing, which is what a form that
    /// re-checks itself on every keystroke needs.
    /// </summary>
    public static bool TryCreate(
        ReadOnlySpan<char> title,
        ReadOnlySpan<char> url,
        ReadOnlySpan<char> body,
        bool isNsfw,
        out PostDraft draft)
    {
        if (!PostTitle.TryCreate(title, out PostTitle headline)
            || !TryReadLink(url, out WebLink? link)
            || !TryReadBody(body, out MarkdownText text))
        {
            draft = default;
            return false;
        }

        draft = new PostDraft(headline, link, text, isNsfw);
        return true;
    }

    /// <summary>Why those three cannot be posted, or <see langword="null"/> when they can.</summary>
    public static string? Explain(ReadOnlySpan<char> title, ReadOnlySpan<char> url, ReadOnlySpan<char> body)
    {
        if (PostTitle.Explain(title) is { } titleReason)
        {
            return titleReason;
        }

        if (ExplainLink(url) is { } linkReason)
        {
            return linkReason;
        }

        return body.Trim().Length > MaxBodyLength
            ? $"a post body can be at most {MaxBodyLength} characters, and this is {body.Trim().Length}"
            : null;
    }

    /// <summary>
    /// Why a link cannot be used, or <see langword="null"/> when it can — including for a blank one,
    /// since a post does not need a link at all. Separate from <see cref="Explain"/> so the link box
    /// can complain about itself while the title is still being typed.
    /// </summary>
    public static string? ExplainLink(ReadOnlySpan<char> url) =>
        TryReadLink(url, out _) ? null : "that link is not a web address";

    /// <summary>
    /// Reads the link box. Blank means no link, and text with no scheme is assumed to be
    /// <c>https</c>: people paste <c>example.com/article</c>, and refusing that would be pedantry
    /// rather than validation.
    /// </summary>
    private static bool TryReadLink(ReadOnlySpan<char> url, out WebLink? link)
    {
        ReadOnlySpan<char> trimmed = url.Trim();
        if (trimmed.IsEmpty)
        {
            link = null;
            return true;
        }

        // The scheme test is on the raw text so that a pasted "ftp://host" is refused rather than
        // quietly turned into "https://ftp://host".
        bool hasScheme = trimmed.Contains("://", StringComparison.Ordinal);
        string? prefixed = hasScheme ? null : string.Concat("https://", trimmed);

        if (!WebLink.TryParse(hasScheme ? trimmed : prefixed.AsSpan(), out WebLink parsed))
        {
            link = null;
            return false;
        }

        link = parsed;
        return true;
    }

    /// <summary>
    /// Reads the body box. Trailing whitespace from a text area is not part of what was written, and
    /// over-long is rejected rather than clipped: this is the author's own text, and silently
    /// dropping the end of it would be the worst possible answer.
    /// </summary>
    private static bool TryReadBody(ReadOnlySpan<char> body, out MarkdownText text)
    {
        ReadOnlySpan<char> trimmed = body.Trim();
        if (trimmed.Length > MaxBodyLength)
        {
            text = MarkdownText.Empty;
            return false;
        }

        return MarkdownText.TryCreate(trimmed, out text);
    }
}
