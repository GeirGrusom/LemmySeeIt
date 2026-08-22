using System.Collections.Immutable;

using Lemmy.Domain;

namespace Lemmy.Domain.Markdown;

/// <summary>
/// One block of a rendered document. A closed hierarchy of records rather than a general tree with
/// a type tag: the renderer switches over these exhaustively, so a block the parser learns to emit
/// cannot be silently dropped by a view that has not been taught to draw it.
/// </summary>
public abstract record MarkdownBlock
{
    private protected MarkdownBlock()
    {
    }
}

/// <summary>Ordinary prose.</summary>
/// <param name="Content">The paragraph's text and styling.</param>
public sealed record MarkdownParagraph(RichText Content) : MarkdownBlock;

/// <summary>A heading.</summary>
/// <param name="Level">1 for <c>#</c> through 6 for <c>######</c>.</param>
/// <param name="Content">The heading's text and styling.</param>
public sealed record MarkdownHeading(int Level, RichText Content) : MarkdownBlock;

/// <summary>A preformatted block.</summary>
/// <param name="Text">The code, exactly as written.</param>
/// <param name="Language">The fence's language hint, when it had one.</param>
public sealed record MarkdownCodeBlock(string Text, string? Language) : MarkdownBlock;

/// <summary>A quotation, which may contain any other blocks.</summary>
/// <param name="Children">What is being quoted.</param>
public sealed record MarkdownQuote(ImmutableArray<MarkdownBlock> Children) : MarkdownBlock;

/// <summary>A bulleted or numbered list.</summary>
/// <param name="IsOrdered">Whether items are numbered rather than bulleted.</param>
/// <param name="Start">The first number, for an ordered list that does not start at one.</param>
/// <param name="Items">Each item's own blocks, so a list item can hold a paragraph and a nested list.</param>
public sealed record MarkdownList(
    bool IsOrdered,
    int Start,
    ImmutableArray<ImmutableArray<MarkdownBlock>> Items) : MarkdownBlock;

/// <summary>
/// Lemmy's spoiler, written as a <c>:::</c> container. Common enough on the sites this app reads
/// that rendering it as literal colons would be a visible failure.
/// </summary>
/// <param name="Title">What the fold says before it is opened.</param>
/// <param name="Children">What it hides.</param>
public sealed record MarkdownSpoiler(string Title, ImmutableArray<MarkdownBlock> Children) : MarkdownBlock;

/// <summary>
/// A picture written into the body. Its own block rather than part of a paragraph: a paragraph is
/// flattened to one continuous string so it wraps and selects as a whole, and a picture is not text.
/// A paragraph with a picture in the middle is split around it.
/// </summary>
/// <param name="Source">Where the picture is.</param>
/// <param name="AltText">What the author called it; empty when they called it nothing.</param>
public sealed record MarkdownImage(WebLink Source, string AltText) : MarkdownBlock;

/// <summary>A horizontal rule.</summary>
public sealed record MarkdownThematicBreak : MarkdownBlock;

/// <summary>A table.</summary>
/// <param name="Header">The header row, empty when the table has none.</param>
/// <param name="Rows">The body rows, each the same width as the widest row.</param>
public sealed record MarkdownTable(
    ImmutableArray<RichText> Header,
    ImmutableArray<ImmutableArray<RichText>> Rows) : MarkdownBlock;
