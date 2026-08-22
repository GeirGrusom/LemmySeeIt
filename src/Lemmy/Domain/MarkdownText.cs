namespace Lemmy.Domain;

/// <summary>
/// A body of Markdown as authored on Lemmy — a post body, a comment, a community sidebar. Empty is
/// a legitimate value (most link posts have no body), so this type validates length rather than
/// presence and offers <see cref="ToPreview"/> for the one-line summaries a feed needs.
/// </summary>
public readonly record struct MarkdownText
{
    /// <summary>The longest body a Lemmy server will accept.</summary>
    public const int MaxLength = 50_000;

    private readonly string? text;

    /// <summary>Validates a Markdown body.</summary>
    /// <exception cref="DomainValidationException">The text is longer than <see cref="MaxLength"/>.</exception>
    public MarkdownText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > MaxLength)
        {
            throw DomainValidationException.For(
                nameof(MarkdownText),
                $"the body is {value.Length} characters, over the {MaxLength} limit");
        }

        text = value;
    }

    /// <summary>An explicitly empty body.</summary>
    public static MarkdownText Empty => default;

    /// <summary>The raw Markdown.</summary>
    public string Value => text ?? string.Empty;

    /// <summary><see langword="true"/> when there is nothing to render.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(text);

    /// <summary>
    /// Validates a body without throwing. Over-long bodies are truncated rather than rejected: a
    /// clipped post body is still worth reading, and a server we do not control set the length.
    /// </summary>
    public static bool TryCreate(ReadOnlySpan<char> value, out MarkdownText markdown)
    {
        markdown = new MarkdownText(value.Length > MaxLength ? value[..MaxLength].ToString() : value.ToString());
        return true;
    }

    /// <summary>
    /// Flattens the Markdown to a single line of at most <paramref name="maxLength"/> characters for
    /// feed previews: link targets, emphasis runs, heading hashes and quote markers are dropped and
    /// every whitespace run becomes one space.
    /// </summary>
    public string ToPreview(int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        ReadOnlySpan<char> source = Value.AsSpan();
        if (source.IsEmpty)
        {
            return string.Empty;
        }

        // One extra slot so we can tell "exactly full" from "ran out", and add an ellipsis if so.
        Span<char> buffer = maxLength < 512 ? stackalloc char[512] : new char[maxLength + 1];
        int written = 0;
        bool previousWasSpace = true;

        for (int index = 0; index < source.Length && written <= maxLength; index++)
        {
            char character = source[index];

            // An image is not text. Its URL is noise in a one-line preview and its alt text, when
            // there is any, is the only readable part — so keep that and drop the rest.
            if (character == '!'
                && index + 1 < source.Length
                && source[index + 1] == '['
                && TryReadLink(source[(index + 1)..], out ReadOnlySpan<char> altText, out _, out int imageLength))
            {
                AppendCollapsed(altText, buffer, maxLength, ref written, ref previousWasSpace);
                index += imageLength;
                continue;
            }

            // A preview has no room for link targets, but the label is the readable part.
            if (character == '[' && TryReadLink(source[index..], out ReadOnlySpan<char> label, out _, out int consumed))
            {
                AppendCollapsed(label, buffer, maxLength, ref written, ref previousWasSpace);
                index += consumed - 1;
                continue;
            }

            // A run of three or more of these is a horizontal rule or one of Lemmy's ":::" spoiler
            // fences. Flattened onto one line they read as line noise, so the whole run goes.
            if (character is '-' or '_' or ':')
            {
                int run = 1;
                while (index + run < source.Length && source[index + run] == character)
                {
                    run++;
                }

                if (run >= 3)
                {
                    index += run - 1;
                    previousWasSpace = true;
                    continue;
                }
            }

            // Brackets that are not part of a link are the author's own; "[Deleted]" should survive.
            if (character is '*' or '_' or '`' or '#' or '>' or '~')
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                previousWasSpace = true;
                continue;
            }

            if (previousWasSpace && written > 0)
            {
                buffer[written++] = ' ';
                if (written > maxLength)
                {
                    break;
                }
            }

            previousWasSpace = false;
            buffer[written++] = character;
        }

        if (written > maxLength)
        {
            return string.Concat(buffer[..maxLength].TrimEnd(), "…");
        }

        return buffer[..written].TrimEnd().ToString();
    }

    /// <summary>
    /// Copies text into the preview buffer, collapsing whitespace runs to single spaces and
    /// stopping at the budget. Shared by link labels and image alt text so the two cannot drift.
    /// </summary>
    private static void AppendCollapsed(
        ReadOnlySpan<char> text,
        Span<char> buffer,
        int maxLength,
        ref int written,
        ref bool previousWasSpace)
    {
        foreach (char character in text)
        {
            if (written > maxLength)
            {
                return;
            }

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
            if (written <= maxLength)
            {
                buffer[written++] = character;
            }
        }
    }

    /// <summary>Matches <c>[label](target)</c> starting at the opening bracket.</summary>
    private static bool TryReadLink(
        ReadOnlySpan<char> source,
        out ReadOnlySpan<char> label,
        out ReadOnlySpan<char> target,
        out int consumed)
    {
        label = default;
        target = default;
        consumed = 0;

        int closeLabel = source.IndexOf(']');
        if (closeLabel < 1 || closeLabel + 1 >= source.Length || source[closeLabel + 1] != '(')
        {
            return false;
        }

        ReadOnlySpan<char> rest = source[(closeLabel + 2)..];
        int closeTarget = rest.IndexOf(')');
        if (closeTarget < 0)
        {
            return false;
        }

        label = source[1..closeLabel];
        target = rest[..closeTarget];
        consumed = closeLabel + 2 + closeTarget + 1;

        // A label that already is the target reads better without the duplicate.
        if (label.SequenceEqual(target))
        {
            target = default;
        }

        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
