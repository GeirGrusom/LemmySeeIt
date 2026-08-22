using System.Collections.Immutable;
using System.Text;
using Markdig.Syntax.Inlines;

namespace Lemmy.Domain.Markdown;

/// <summary>
/// Flattens Markdig's inline tree into one string plus the styled ranges over it.
/// </summary>
/// <remarks>
/// The flattening is the point. Emphasis nests, but the text it decorates has to stay one
/// continuous string so a paragraph wraps and selects as a whole — so nesting is turned into
/// overlapping-free ranges as the tree is walked, with the current style carried down.
/// </remarks>
internal static class InlineReader
{
    /// <summary>Reads a paragraph's inlines. A null container is an empty paragraph.</summary>
    internal static RichText Read(ContainerInline? container)
    {
        if (container is null)
        {
            return RichText.Empty;
        }

        var text = new StringBuilder();
        var spans = ImmutableArray.CreateBuilder<MarkdownSpan>();

        Walk(container, text, spans, MarkdownSpanStyle.None, null);

        string result = text.ToString().TrimEnd();
        if (result.Length == 0)
        {
            return RichText.Empty;
        }

        // Trimming may have cut into the last span.
        for (int index = spans.Count - 1; index >= 0; index--)
        {
            MarkdownSpan span = spans[index];
            if (span.Start >= result.Length)
            {
                spans.RemoveAt(index);
            }
            else if (span.End > result.Length)
            {
                spans[index] = span with { Length = result.Length - span.Start };
            }
        }

        return new RichText(result, spans.ToImmutable());
    }

    private static void Walk(
        ContainerInline container,
        StringBuilder text,
        ImmutableArray<MarkdownSpan>.Builder spans,
        MarkdownSpanStyle style,
        WebLink? link)
    {
        foreach (Inline inline in container)
        {
            Append(inline, text, spans, style, link);
        }
    }

    private static void Append(
        Inline inline,
        StringBuilder text,
        ImmutableArray<MarkdownSpan>.Builder spans,
        MarkdownSpanStyle style,
        WebLink? link)
    {
        switch (inline)
        {
            case LiteralInline literal:
                Emit(literal.Content.ToString(), text, spans, style, link);
                break;

            case CodeInline code:
                Emit(code.Content, text, spans, style | MarkdownSpanStyle.Code, link);
                break;

            case LineBreakInline lineBreak:
                // A hard break is two trailing spaces and means it; a soft one is just wrapping in
                // the source, which should not become a line break on screen.
                text.Append(lineBreak.IsHard ? '\n' : ' ');
                break;

            case LinkInline { IsImage: true } image:
                EmitImage(image, text, spans, style);
                break;

            case LinkInline anchor:
                EmitLink(anchor, text, spans, style, link);
                break;

            case AutolinkInline autolink:
                Emit(
                    autolink.Url,
                    text,
                    spans,
                    style,
                    WebLink.TryParse(autolink.Url.AsSpan(), out WebLink parsed) ? parsed : link);
                break;

            case EmphasisInline emphasis:
                Walk(emphasis, text, spans, style | StyleOf(emphasis), link);
                break;

            case ContainerInline nested:
                Walk(nested, text, spans, style, link);
                break;

            case HtmlInline or HtmlEntityInline:
                // Lemmy strips raw HTML server-side; anything left is not worth rendering as markup.
                break;

            default:
                break;
        }
    }

    /// <summary>An image becomes its alt text, linked to the picture, rather than nothing at all.</summary>
    private static void EmitImage(
        LinkInline image,
        StringBuilder text,
        ImmutableArray<MarkdownSpan>.Builder spans,
        MarkdownSpanStyle style)
    {
        WebLink? target = WebLink.TryParse(image.Url.AsSpan(), out WebLink parsed) ? parsed : null;

        string label = DescribeImage(image);
        Emit(label, text, spans, style, target);
    }

    private static string DescribeImage(LinkInline image)
    {
        var alt = new StringBuilder();
        foreach (Inline child in image)
        {
            if (child is LiteralInline literal)
            {
                alt.Append(literal.Content.ToString());
            }
        }

        string described = alt.ToString().Trim();

        return described.Length > 0 ? described : "image";
    }

    private static void EmitLink(
        LinkInline anchor,
        StringBuilder text,
        ImmutableArray<MarkdownSpan>.Builder spans,
        MarkdownSpanStyle style,
        WebLink? inherited)
    {
        WebLink? target = WebLink.TryParse(anchor.Url.AsSpan(), out WebLink parsed) ? parsed : inherited;

        int before = text.Length;
        Walk(anchor, text, spans, style, target);

        // A link whose label was empty still needs something to press.
        if (text.Length == before && target is { } fallback)
        {
            Emit(fallback.Value, text, spans, style, fallback);
        }
    }

    private static void Emit(
        string content,
        StringBuilder text,
        ImmutableArray<MarkdownSpan>.Builder spans,
        MarkdownSpanStyle style,
        WebLink? link)
    {
        if (content.Length == 0)
        {
            return;
        }

        int start = text.Length;
        text.Append(content);

        if (style != MarkdownSpanStyle.None || link.HasValue)
        {
            spans.Add(new MarkdownSpan(start, content.Length, style, link));
        }
    }

    private static MarkdownSpanStyle StyleOf(EmphasisInline emphasis) => emphasis.DelimiterChar switch
    {
        '*' or '_' => emphasis.DelimiterCount >= 2 ? MarkdownSpanStyle.Bold : MarkdownSpanStyle.Italic,
        '~' => emphasis.DelimiterCount >= 2 ? MarkdownSpanStyle.Strikethrough : MarkdownSpanStyle.Subscript,
        '^' => MarkdownSpanStyle.Superscript,
        _ => MarkdownSpanStyle.None,
    };
}
