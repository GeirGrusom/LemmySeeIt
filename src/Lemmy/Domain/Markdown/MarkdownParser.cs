using System.Collections.Immutable;
using System.Text;
using Markdig;
using Markdig.Extensions.CustomContainers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Lemmy.Domain.Markdown;

/// <summary>
/// Turns Lemmy's Markdown into this app's own block model.
/// </summary>
/// <remarks>
/// Markdig does the parsing and is kept behind this wall: nothing outside sees its types, so the
/// renderer stays a plain mapper over immutable records and swapping the parser later would touch
/// only this file. The enabled extensions mirror what Lemmy's own frontend turns on, which is why
/// strikethrough, superscript and <c>:::</c> spoilers are here and, say, footnotes are not.
/// </remarks>
public static class MarkdownParser
{
    private const int MaxBlockDepth = 12;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .UseCustomContainers()
        .UsePipeTables()
        .UseListExtras()
        .Build();

    /// <summary>Parses a body into blocks. Never throws; unparseable input becomes one paragraph.</summary>
    public static ImmutableArray<MarkdownBlock> Parse(MarkdownText markdown)
    {
        if (markdown.IsEmpty)
        {
            return [];
        }

        try
        {
            MarkdownDocument document = Markdig.Markdown.Parse(markdown.Value, Pipeline);
            return ConvertBlocks(document, depth: 0);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A body that cannot be parsed is still worth reading as text.
            return [new MarkdownParagraph(new RichText(markdown.Value, []))];
        }
    }

    private static ImmutableArray<MarkdownBlock> ConvertBlocks(ContainerBlock container, int depth)
    {
        var blocks = ImmutableArray.CreateBuilder<MarkdownBlock>(container.Count);

        foreach (Block child in container)
        {
            // A paragraph can come back as several blocks, because a picture written into it is
            // lifted out and the text either side stays a paragraph.
            if (child is ParagraphBlock paragraph)
            {
                blocks.AddRange(InlineReader.ReadBlocks(paragraph.Inline));
                continue;
            }

            if (Convert(child, depth) is { } block)
            {
                blocks.Add(block);
            }
        }

        return blocks.ToImmutable();
    }

    private static MarkdownBlock? Convert(Block block, int depth)
    {
        // Deeply nested quotes and lists are usually someone quoting a quote of a quote; past a
        // point the indentation costs more than the structure conveys.
        if (depth > MaxBlockDepth)
        {
            return null;
        }

        return block switch
        {
            HeadingBlock heading => new MarkdownHeading(
                Math.Clamp(heading.Level, 1, 6),
                InlineReader.Read(heading.Inline)),
            CodeBlock code => ConvertCode(code),
            CustomContainer spoiler => ConvertSpoiler(spoiler, depth),
            QuoteBlock quote => new MarkdownQuote(ConvertBlocks(quote, depth + 1)),
            ListBlock list => ConvertList(list, depth),
            ThematicBreakBlock => new MarkdownThematicBreak(),
            Markdig.Extensions.Tables.Table table => ConvertTable(table),
            _ => null,
        };
    }

    private static MarkdownBlock ConvertCode(CodeBlock code)
    {
        var text = new StringBuilder();
        foreach (Markdig.Helpers.StringLine line in code.Lines.Lines)
        {
            if (line.Slice.Text is null)
            {
                continue;
            }

            text.AppendLine(line.Slice.ToString());
        }

        string? language = (code as FencedCodeBlock)?.Info;

        return new MarkdownCodeBlock(
            text.ToString().TrimEnd('\n', '\r'),
            string.IsNullOrWhiteSpace(language) ? null : language);
    }

    /// <summary>
    /// Lemmy writes a spoiler as <c>::: spoiler Title</c>. Anything else fenced with colons is a
    /// container we have no special meaning for, so its contents are shown rather than hidden.
    /// </summary>
    private static MarkdownBlock ConvertSpoiler(CustomContainer container, int depth)
    {
        string info = container.Info ?? string.Empty;
        string arguments = container.Arguments ?? string.Empty;

        ImmutableArray<MarkdownBlock> children = ConvertBlocks(container, depth + 1);

        if (!info.Equals("spoiler", StringComparison.OrdinalIgnoreCase))
        {
            return new MarkdownQuote(children);
        }

        string title = string.IsNullOrWhiteSpace(arguments) ? "Spoiler" : arguments.Trim();

        return new MarkdownSpoiler(title, children);
    }

    private static MarkdownBlock ConvertList(ListBlock list, int depth)
    {
        var items = ImmutableArray.CreateBuilder<ImmutableArray<MarkdownBlock>>(list.Count);

        foreach (Block child in list)
        {
            items.Add(child is ListItemBlock item ? ConvertBlocks(item, depth + 1) : []);
        }

        int start = 1;
        if (list.IsOrdered && int.TryParse(list.OrderedStart, out int parsed))
        {
            start = parsed;
        }

        return new MarkdownList(list.IsOrdered, start, items.ToImmutable());
    }

    private static MarkdownBlock ConvertTable(Markdig.Extensions.Tables.Table table)
    {
        ImmutableArray<RichText> header = [];
        var rows = ImmutableArray.CreateBuilder<ImmutableArray<RichText>>();

        foreach (Block child in table)
        {
            if (child is not Markdig.Extensions.Tables.TableRow row)
            {
                continue;
            }

            var cells = ImmutableArray.CreateBuilder<RichText>(row.Count);
            foreach (Block cellBlock in row)
            {
                cells.Add(cellBlock is Markdig.Extensions.Tables.TableCell cell && cell.Count > 0
                    ? ReadCell(cell)
                    : RichText.Empty);
            }

            if (row.IsHeader && header.IsEmpty)
            {
                header = cells.ToImmutable();
            }
            else
            {
                rows.Add(cells.ToImmutable());
            }
        }

        return new MarkdownTable(header, rows.ToImmutable());
    }

    private static RichText ReadCell(Markdig.Extensions.Tables.TableCell cell) =>
        cell[0] is ParagraphBlock paragraph ? InlineReader.Read(paragraph.Inline) : RichText.Empty;
}
