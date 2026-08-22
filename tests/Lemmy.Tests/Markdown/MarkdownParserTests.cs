using System.Collections.Immutable;
using Lemmy.Domain;
using Lemmy.Domain.Markdown;

namespace Lemmy.Tests.Markdown;

[TestFixture]
internal sealed class MarkdownParserTests
{
    private static ImmutableArray<MarkdownBlock> Parse(string source) =>
        MarkdownParser.Parse(new MarkdownText(source));

    private static RichText ParagraphText(string source) =>
        ((MarkdownParagraph)Parse(source).Single()).Content;

    private static MarkdownSpanStyle StyleAt(RichText text, int index)
    {
        MarkdownSpanStyle style = MarkdownSpanStyle.None;
        foreach (MarkdownSpan span in text.Spans)
        {
            if (span.Contains(index))
            {
                style |= span.Style;
            }
        }

        return style;
    }

    [Test]
    public void AnEmptyBodyHasNoBlocks() =>
        Assert.That(MarkdownParser.Parse(MarkdownText.Empty), Is.Empty);

    [Test]
    public void ProseBecomesAParagraphWithTheSyntaxRemoved()
    {
        RichText text = ParagraphText("Just some **plain** prose.");

        Assert.That(text.Text, Is.EqualTo("Just some plain prose."));
    }

    [Test]
    public void EmphasisIsRecordedAsARangeRatherThanLeftInTheText()
    {
        RichText text = ParagraphText("a **bold** c");

        Assert.Multiple(() =>
        {
            Assert.That(text.Text, Is.EqualTo("a bold c"));
            Assert.That(StyleAt(text, 2), Is.EqualTo(MarkdownSpanStyle.Bold));
            Assert.That(StyleAt(text, 0), Is.EqualTo(MarkdownSpanStyle.None));
        });
    }

    [TestCase("*x*", MarkdownSpanStyle.Italic)]
    [TestCase("_x_", MarkdownSpanStyle.Italic)]
    [TestCase("**x**", MarkdownSpanStyle.Bold)]
    [TestCase("~~x~~", MarkdownSpanStyle.Strikethrough)]
    [TestCase("`x`", MarkdownSpanStyle.Code)]
    [TestCase("^x^", MarkdownSpanStyle.Superscript)]
    [TestCase("~x~", MarkdownSpanStyle.Subscript)]
    public void EachKindOfEmphasisIsRecognised(string source, MarkdownSpanStyle expected)
    {
        RichText text = ParagraphText(source);

        Assert.Multiple(() =>
        {
            Assert.That(text.Text, Is.EqualTo("x"));
            Assert.That(StyleAt(text, 0), Is.EqualTo(expected));
        });
    }

    /// <summary>Markdown nests emphasis freely; flattening must not lose the outer style.</summary>
    [Test]
    public void NestedEmphasisCombines()
    {
        RichText text = ParagraphText("**bold and *also italic* here**");

        Assert.Multiple(() =>
        {
            Assert.That(text.Text, Is.EqualTo("bold and also italic here"));
            Assert.That(StyleAt(text, 0), Is.EqualTo(MarkdownSpanStyle.Bold));
            Assert.That(
                StyleAt(text, text.Text.IndexOf("also", StringComparison.Ordinal)),
                Is.EqualTo(MarkdownSpanStyle.Bold | MarkdownSpanStyle.Italic));
        });
    }

    [Test]
    public void ALinkKeepsItsLabelAndCarriesItsTarget()
    {
        RichText text = ParagraphText("see [the docs](https://example.com/a) for more");

        Assert.Multiple(() =>
        {
            Assert.That(text.Text, Is.EqualTo("see the docs for more"));
            Assert.That(text.LinkAt(4)?.Value, Is.EqualTo("https://example.com/a"));
            Assert.That(text.LinkAt(0), Is.Null, "the prose around it is not a link");
        });
    }

    [Test]
    public void ABareUrlBecomesALink()
    {
        RichText text = ParagraphText("go to https://example.com/x now");

        Assert.That(text.LinkAt(text.Text.IndexOf("https", StringComparison.Ordinal))?.Value,
            Is.EqualTo("https://example.com/x"));
    }

    /// <summary>A relative or unusable target must not become a tappable link that goes nowhere.</summary>
    [Test]
    public void ALinkToSomethingUnusableIsNotTappable()
    {
        RichText text = ParagraphText("[label](/relative/path)");

        Assert.Multiple(() =>
        {
            Assert.That(text.Text, Is.EqualTo("label"));
            Assert.That(text.LinkAt(0), Is.Null);
        });
    }

    [Test]
    public void AnImageBecomesAPictureRatherThanALinkToOne()
    {
        ImmutableArray<MarkdownBlock> blocks = Parse("![a chart](https://example.com/c.png)");

        Assert.That(blocks, Has.Length.EqualTo(1));
        var image = (MarkdownImage)blocks[0];

        Assert.Multiple(() =>
        {
            Assert.That(image.Source.Value, Is.EqualTo("https://example.com/c.png"));
            Assert.That(image.AltText, Is.EqualTo("a chart"));
        });
    }

    [Test]
    public void AnImageWithNoAltTextCarriesNoneRatherThanAStandIn()
    {
        // What to call a nameless picture is the renderer's business.
        ImmutableArray<MarkdownBlock> blocks = Parse("![](https://example.com/c.png)");

        Assert.That(((MarkdownImage)blocks[0]).AltText, Is.Empty);
    }

    [Test]
    public void TextEitherSideOfAPictureStaysAParagraph()
    {
        ImmutableArray<MarkdownBlock> blocks = Parse("before ![a chart](https://example.com/c.png) after");

        Assert.That(blocks, Has.Length.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(((MarkdownParagraph)blocks[0]).Content.Text, Is.EqualTo("before"));
            Assert.That(((MarkdownImage)blocks[1]).Source.Value, Is.EqualTo("https://example.com/c.png"));
            Assert.That(((MarkdownParagraph)blocks[2]).Content.Text, Is.EqualTo("after"));
        });
    }

    [Test]
    public void SeveralPicturesInOneParagraphEachBecomeTheirOwnBlock()
    {
        ImmutableArray<MarkdownBlock> blocks =
            Parse("![one](https://example.com/1.png) ![two](https://example.com/2.png)");

        Assert.That(blocks.OfType<MarkdownImage>().Count(), Is.EqualTo(2));
    }

    [Test]
    public void APictureInsideALinkStaysTheLinkItWasWrittenAs()
    {
        // The author wrote a link and used a picture as its label; the link is the point.
        ImmutableArray<MarkdownBlock> blocks =
            Parse("[![a chart](https://example.com/c.png)](https://example.com/page)");

        Assert.Multiple(() =>
        {
            Assert.That(blocks.OfType<MarkdownImage>(), Is.Empty);
            Assert.That(((MarkdownParagraph)blocks[0]).Content.Text, Is.EqualTo("a chart"));
        });
    }

    [Test]
    public void APictureWithAnUnusableAddressIsLeftAsText()
    {
        ImmutableArray<MarkdownBlock> blocks = Parse("![broken](not-a-url)");

        Assert.Multiple(() =>
        {
            Assert.That(blocks.OfType<MarkdownImage>(), Is.Empty);
            Assert.That(((MarkdownParagraph)blocks[0]).Content.Text, Is.EqualTo("broken"));
        });
    }

    [Test]
    public void APictureInsideAQuoteIsStillAPicture()
    {
        ImmutableArray<MarkdownBlock> blocks = Parse("> ![a chart](https://example.com/c.png)");

        var quote = (MarkdownQuote)blocks[0];
        Assert.That(quote.Children.OfType<MarkdownImage>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void HeadingsKeepTheirLevel()
    {
        ImmutableArray<MarkdownBlock> blocks = Parse("# One\n\n### Three");

        Assert.Multiple(() =>
        {
            Assert.That(((MarkdownHeading)blocks[0]).Level, Is.EqualTo(1));
            Assert.That(((MarkdownHeading)blocks[0]).Content.Text, Is.EqualTo("One"));
            Assert.That(((MarkdownHeading)blocks[1]).Level, Is.EqualTo(3));
        });
    }

    [Test]
    public void AFencedBlockKeepsItsTextExactlyAndItsLanguage()
    {
        var code = (MarkdownCodeBlock)Parse("```csharp\nvar x = 1;\n  indented\n```").Single();

        Assert.Multiple(() =>
        {
            Assert.That(code.Language, Is.EqualTo("csharp"));
            Assert.That(code.Text, Is.EqualTo("var x = 1;\n  indented"));
        });
    }

    [Test]
    public void AnUnlabelledFenceHasNoLanguage()
    {
        var code = (MarkdownCodeBlock)Parse("```\nplain\n```").Single();

        Assert.That(code.Language, Is.Null);
    }

    [Test]
    public void QuotesNestTheirContents()
    {
        var quote = (MarkdownQuote)Parse("> quoted **text**").Single();

        Assert.That(((MarkdownParagraph)quote.Children.Single()).Content.Text, Is.EqualTo("quoted text"));
    }

    [Test]
    public void BulletedListsKeepTheirItems()
    {
        var list = (MarkdownList)Parse("- one\n- two\n- three").Single();

        Assert.Multiple(() =>
        {
            Assert.That(list.IsOrdered, Is.False);
            Assert.That(list.Items, Has.Length.EqualTo(3));
            Assert.That(((MarkdownParagraph)list.Items[1].Single()).Content.Text, Is.EqualTo("two"));
        });
    }

    [Test]
    public void NumberedListsKeepWhereTheyStart()
    {
        var list = (MarkdownList)Parse("3. three\n4. four").Single();

        Assert.Multiple(() =>
        {
            Assert.That(list.IsOrdered, Is.True);
            Assert.That(list.Start, Is.EqualTo(3));
        });
    }

    [Test]
    public void AListItemCanHoldAListOfItsOwn()
    {
        var list = (MarkdownList)Parse("- outer\n    - inner").Single();

        Assert.That(list.Items[0].OfType<MarkdownList>().Single().Items, Has.Length.EqualTo(1));
    }

    /// <summary>Lemmy's spoiler syntax. Rendering this as literal colons would be a visible failure.</summary>
    [Test]
    public void ALemmySpoilerBecomesAFold()
    {
        var spoiler = (MarkdownSpoiler)Parse("::: spoiler The answer\nforty two\n:::").Single();

        Assert.Multiple(() =>
        {
            Assert.That(spoiler.Title, Is.EqualTo("The answer"));
            Assert.That(((MarkdownParagraph)spoiler.Children.Single()).Content.Text, Is.EqualTo("forty two"));
        });
    }

    [Test]
    public void ASpoilerWithNoTitleStillSaysWhatItIs()
    {
        var spoiler = (MarkdownSpoiler)Parse("::: spoiler\nhidden\n:::").Single();

        Assert.That(spoiler.Title, Is.EqualTo("Spoiler"));
    }

    [Test]
    public void AThematicBreakIsItsOwnBlock() =>
        Assert.That(Parse("above\n\n---\n\nbelow")[1], Is.TypeOf<MarkdownThematicBreak>());

    [Test]
    public void ATableKeepsItsHeaderAndRows()
    {
        var table = (MarkdownTable)Parse("| a | b |\n|---|---|\n| 1 | 2 |").Single();

        Assert.Multiple(() =>
        {
            Assert.That(table.Header.Select(cell => cell.Text), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(table.Rows, Has.Length.EqualTo(1));
            Assert.That(table.Rows[0].Select(cell => cell.Text), Is.EqualTo(new[] { "1", "2" }));
        });
    }

    /// <summary>
    /// A newline in the source is wrapping, not a line break; two trailing spaces mean it.
    /// </summary>
    [Test]
    public void ASoftWrapDoesNotBecomeALineBreak()
    {
        RichText text = ParagraphText("one\ntwo");

        Assert.That(text.Text, Is.EqualTo("one two"));
    }

    [Test]
    public void AHardBreakIsKept()
    {
        RichText text = ParagraphText("one  \ntwo");

        Assert.That(text.Text, Is.EqualTo("one\ntwo"));
    }

    [Test]
    public void SpansNeverPointPastTheirText()
    {
        foreach (MarkdownBlock block in Parse("**bold**   \n\n*trailing italic*   "))
        {
            if (block is MarkdownParagraph paragraph)
            {
                foreach (MarkdownSpan span in paragraph.Content.Spans)
                {
                    Assert.That(span.End, Is.LessThanOrEqualTo(paragraph.Content.Text.Length));
                }
            }
        }
    }

    [Test]
    public void RidiculouslyNestedQuotesStopSomewhere()
    {
        string nested = string.Concat(Enumerable.Repeat("> ", 40)) + "deep";

        Assert.That(() => Parse(nested), Throws.Nothing);
    }
}
