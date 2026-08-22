using System.Collections.Immutable;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Domain;
using Lemmy.Domain.Markdown;
using Lemmy.Views.Markdown;

namespace Lemmy.Tests.Ui;

/// <summary>
/// Drawing parsed Markdown. The parser is covered on its own; these are about what ends up on
/// screen and what happens when it is pressed.
/// </summary>
[TestFixture]
internal sealed class MarkdownRenderingTests
{
    private static void Settle() => Dispatcher.UIThread.RunJobs();

    private static (Window Window, MarkdownView View) Render(string source, ICommand? linkCommand = null)
    {
        var view = new MarkdownView
        {
            Blocks = MarkdownParser.Parse(new MarkdownText(source)),
            LinkCommand = linkCommand,
            Width = 400,
        };

        var window = new Window { Width = 420, Height = 700, Content = view };
        window.Show();
        Settle();

        return (window, view);
    }

    /// <summary>
    /// The point on screen of one character. A paragraph fills its width but its text may not, so
    /// aiming at the middle of the block usually lands past the end of the words.
    /// </summary>
    private static Point GlyphPoint(TextBlock block, Window window, int characterIndex)
    {
        Rect glyph = block.TextLayout.HitTestTextPosition(characterIndex);
        var local = new Point(glyph.X + (glyph.Width / 2), glyph.Y + (glyph.Height / 2));

        return block.TranslatePoint(local, window) ?? default;
    }

    private static IEnumerable<TextBlock> TextBlocks(Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>();

    private static string AllText(Visual root) =>
        string.Join("\n", TextBlocks(root).Select(block => block.Inlines?.Text ?? block.Text).Where(text => !string.IsNullOrEmpty(text)));

    [AvaloniaTest]
    public void ProseRendersWithoutItsSyntax()
    {
        (Window window, _) = Render("Some **bold** and *italic* prose.");

        Assert.That(AllText(window), Does.Contain("Some bold and italic prose."));
    }

    [AvaloniaTest]
    public void EmphasisBecomesActualFontStyling()
    {
        (Window window, _) = Render("plain **bold** plain");

        var runs = TextBlocks(window)
            .SelectMany(block => block.Inlines ?? [])
            .OfType<Avalonia.Controls.Documents.Run>()
            .ToList();

        Assert.That(
            runs.Any(run => run.Text == "bold" && run.FontWeight == FontWeight.Bold),
            Is.True,
            "the bold run should be drawn bold, not left as asterisks");
    }

    [AvaloniaTest]
    public void ACodeBlockIsPreformattedAndDoesNotWrap()
    {
        (Window window, _) = Render("```\nvar x = 1;\n```");

        TextBlock code = TextBlocks(window).Single(block => (block.Text ?? string.Empty).Contains("var x = 1;", StringComparison.Ordinal));

        Assert.That(code.TextWrapping, Is.EqualTo(TextWrapping.NoWrap));
    }

    [AvaloniaTest]
    public void AListDrawsAMarkerPerItem()
    {
        (Window window, _) = Render("- one\n- two\n- three");

        Assert.That(TextBlocks(window).Count(block => block.Text == "•"), Is.EqualTo(3));
    }

    [AvaloniaTest]
    public void ANumberedListCountsFromWhereItStarts()
    {
        (Window window, _) = Render("3. three\n4. four");

        Assert.That(
            TextBlocks(window).Select(block => block.Text),
            Does.Contain("3.").And.Contain("4."));
    }

    /// <summary>A spoiler is a fold, and it starts folded — otherwise it has spoiled something.</summary>
    [AvaloniaTest]
    public void ASpoilerStartsClosed()
    {
        (Window window, _) = Render("::: spoiler The answer\nforty two\n:::");

        Expander expander = window.GetVisualDescendants().OfType<Expander>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(expander.Header, Is.EqualTo("The answer"));
            Assert.That(expander.IsExpanded, Is.False);
        });
    }

    [AvaloniaTest]
    public void OpeningASpoilerRevealsIt()
    {
        (Window window, _) = Render("::: spoiler s\nforty two\n:::");
        Expander expander = window.GetVisualDescendants().OfType<Expander>().Single();

        expander.IsExpanded = true;
        Settle();

        Assert.That(AllText(window), Does.Contain("forty two"));
    }

    [AvaloniaTest]
    public void ALinkIsDrawnAsOne()
    {
        (Window window, _) = Render("see [the docs](https://example.com/a)");

        var linkRun = TextBlocks(window)
            .SelectMany(block => block.Inlines ?? [])
            .OfType<Avalonia.Controls.Documents.Run>()
            .FirstOrDefault(run => run.Text == "the docs");

        Assert.That(linkRun, Is.Not.Null);
        Assert.That(linkRun!.TextDecorations, Is.EqualTo(TextDecorations.Underline));
    }

    /// <summary>
    /// The whole point of rendering rather than flattening: pressing a link follows it. The press
    /// is aimed at the link's own glyphs, found by measuring the text.
    /// </summary>
    [AvaloniaTest]
    public void PressingALinkFollowsIt()
    {
        WebLink? followed = null;
        var command = new RelayCommand<WebLink>(link => followed = link);

        (Window window, MarkdownView view) = Render("[the docs](https://example.com/a)", command);

        TextBlock block = TextBlocks(view).Single(candidate => (candidate.Inlines?.Text ?? string.Empty).Contains("docs", StringComparison.Ordinal));
        Point onTheLink = GlyphPoint(block, window, 3);

        window.MouseDown(onTheLink, MouseButton.Left);
        window.MouseUp(onTheLink, MouseButton.Left);
        Settle();

        Assert.That(followed?.Value, Is.EqualTo("https://example.com/a"));
    }

    /// <summary>
    /// The near edge of a link still follows it. Hit-testing a point to a character gives a caret
    /// position, which rounds to the nearest boundary — so a tap on the left half of a link's first
    /// letter reports the character before the link and used to do nothing at all.
    /// </summary>
    [AvaloniaTest]
    public void PressingTheVeryStartOfALinkFollowsIt()
    {
        WebLink? followed = null;
        var command = new RelayCommand<WebLink>(link => followed = link);

        (Window window, MarkdownView view) = Render("[the docs](https://example.com/a)", command);

        TextBlock block = TextBlocks(view).Single(candidate => (candidate.Inlines?.Text ?? string.Empty).Contains("docs", StringComparison.Ordinal));
        Rect first = block.TextLayout.HitTestTextPosition(0);
        Point nearEdge = block.TranslatePoint(new Point(first.X + 1, first.Y + (first.Height / 2)), window) ?? default;

        window.MouseDown(nearEdge, MouseButton.Left);
        window.MouseUp(nearEdge, MouseButton.Left);
        Settle();

        Assert.That(followed?.Value, Is.EqualTo("https://example.com/a"));
    }

    /// <summary>A link that wraps across lines is tappable on both of them.</summary>
    [AvaloniaTest]
    public void ALinkThatWrapsIsTappableOnItsSecondLine()
    {
        WebLink? followed = null;
        var command = new RelayCommand<WebLink>(link => followed = link);

        (Window window, MarkdownView view) = Render(
            "[a deliberately long link label that will certainly wrap onto a second line of the paragraph](https://example.com/a)",
            command);

        TextBlock block = TextBlocks(view).First();
        int lastIndex = (block.Inlines?.Text ?? string.Empty).Length - 2;
        Point onSecondLine = GlyphPoint(block, window, lastIndex);

        window.MouseDown(onSecondLine, MouseButton.Left);
        window.MouseUp(onSecondLine, MouseButton.Left);
        Settle();

        Assert.That(followed?.Value, Is.EqualTo("https://example.com/a"));
    }

    /// <summary>Prose next to a link is not the link, however close it is.</summary>
    [AvaloniaTest]
    public void PressingPlainTextFollowsNothing()
    {
        WebLink? followed = null;
        var command = new RelayCommand<WebLink>(link => followed = link);

        (Window window, MarkdownView view) = Render("plain words with no link at all here", command);

        TextBlock block = TextBlocks(view).First();
        Point onTheWords = GlyphPoint(block, window, 3);

        window.MouseDown(onTheWords, MouseButton.Left);
        window.MouseUp(onTheWords, MouseButton.Left);
        Settle();

        Assert.That(followed, Is.Null);
    }

    /// <summary>
    /// Dragging across text is a selection. Following the link under the finger at the end of it
    /// would make a post impossible to quote from.
    /// </summary>
    [AvaloniaTest]
    public void DraggingAcrossALinkSelectsRatherThanFollows()
    {
        WebLink? followed = null;
        var command = new RelayCommand<WebLink>(link => followed = link);

        (Window window, MarkdownView view) = Render("[the docs](https://example.com/a)", command);

        TextBlock block = TextBlocks(view).Single(candidate => (candidate.Inlines?.Text ?? string.Empty).Contains("docs", StringComparison.Ordinal));
        Point start = GlyphPoint(block, window, 0);
        Point end = GlyphPoint(block, window, 7);

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        window.MouseUp(end, MouseButton.Left);
        Settle();

        Assert.That(followed, Is.Null);
    }

    [AvaloniaTest]
    public void AnEmptyDocumentDrawsNothing()
    {
        var view = new MarkdownView { Blocks = [] };
        var window = new Window { Content = view, Width = 200, Height = 100 };
        window.Show();
        Settle();

        Assert.That(view.Child, Is.Null);
    }

    [AvaloniaTest]
    public void ATableDrawsItsCells()
    {
        (Window window, _) = Render("| a | b |\n|---|---|\n| 1 | 2 |");

        string text = AllText(window);

        Assert.That(text, Does.Contain("a").And.Contain("b").And.Contain("1").And.Contain("2"));
    }

    /// <summary>Every block kind the parser can emit has to draw as something.</summary>
    [AvaloniaTest]
    public void EveryKindOfBlockDrawsSomething()
    {
        const string everything = """
            # Heading

            Prose with **bold**, `code`, ~~struck~~ and [a link](https://example.com).

            > a quote

            - a list
            - of items

            1. numbered
            2. items

            ```csharp
            var code = true;
            ```

            ---

            ::: spoiler hidden
            secret
            :::

            | a | b |
            |---|---|
            | 1 | 2 |
            """;

        (Window window, MarkdownView view) = Render(everything);

        Assert.Multiple(() =>
        {
            Assert.That(view.Blocks, Has.Length.EqualTo(9), "heading, prose, quote, two lists, code, rule, spoiler, table");
            Assert.That(view.Child, Is.Not.Null);
            Assert.That(AllText(window), Does.Contain("Heading").And.Contain("a quote").And.Contain("numbered"));
        });
    }
}
