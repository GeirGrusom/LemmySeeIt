using System.Collections.Immutable;

namespace Lemmy.Domain.Markdown;

/// <summary>
/// A run of styled text within a paragraph, expressed as an offset into the parent's plain text
/// rather than as a nested tree.
/// </summary>
/// <remarks>
/// Flat ranges rather than nesting on purpose. The text has to stay one continuous string so that
/// it wraps and can be selected as a whole; carrying the styling alongside as offsets keeps that
/// intact, and makes "which link is under this character" a lookup rather than a tree walk.
/// </remarks>
/// <param name="Start">Index into <see cref="RichText.Text"/> where the run begins.</param>
/// <param name="Length">How many characters it covers.</param>
/// <param name="Style">How to draw it.</param>
/// <param name="Link">Where it goes when tapped, if it goes anywhere.</param>
public readonly record struct MarkdownSpan(int Start, int Length, MarkdownSpanStyle Style, WebLink? Link)
{
    /// <summary>One past the last character the run covers.</summary>
    public int End => Start + Length;

    /// <summary>Whether the run is something the reader can follow.</summary>
    public bool IsLink => Link.HasValue;

    /// <summary>Whether <paramref name="index"/> falls inside the run.</summary>
    public bool Contains(int index) => index >= Start && index < End;
}

/// <summary>A paragraph's worth of text, plus how it is styled.</summary>
/// <param name="Text">The text as it reads, with all Markdown syntax removed.</param>
/// <param name="Spans">Styled runs over that text, in order and never overlapping.</param>
public sealed record RichText(string Text, ImmutableArray<MarkdownSpan> Spans)
{
    /// <summary>Nothing at all.</summary>
    public static RichText Empty { get; } = new(string.Empty, []);

    /// <summary>Whether there is anything to draw.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Text);

    /// <summary>The link at <paramref name="index"/>, if the character there is part of one.</summary>
    public WebLink? LinkAt(int index)
    {
        foreach (MarkdownSpan span in Spans)
        {
            if (span.IsLink && span.Contains(index))
            {
                return span.Link;
            }
        }

        return null;
    }
}
