using Lemmy.Domain;

namespace Lemmy.Tests.Domain;

[TestFixture]
internal sealed class TextTests
{
    [Test]
    public void PostTitle_CollapsesWhitespaceRatherThanRejectingIt()
    {
        PostTitle.TryCreate("  This  HAS\nto be\tsatire ", out PostTitle title);

        Assert.That(title.Value, Is.EqualTo("This HAS to be satire"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\n\t")]
    public void PostTitle_TryCreate_RejectsABlankTitle(string text) =>
        Assert.That(PostTitle.TryCreate(text, out _), Is.False);

    [Test]
    public void PostTitle_TryCreate_RejectsAnOverLongTitle() =>
        Assert.That(PostTitle.TryCreate(new string('a', PostTitle.MaxLength + 1), out _), Is.False);

    /// <summary>
    /// Federated posts arrive from software with its own limits. Dropping such a post from the feed
    /// would be worse than clipping its headline, so the mapper's entry point truncates.
    /// </summary>
    [Test]
    public void PostTitle_CreateTruncated_ClipsRatherThanDropping()
    {
        PostTitle? title = PostTitle.CreateTruncated(new string('a', PostTitle.MaxLength + 50));

        Assert.That(title?.Value, Has.Length.LessThanOrEqualTo(PostTitle.MaxLength));
    }

    [Test]
    public void PostTitle_CreateTruncated_ReturnsNullForBlankText() =>
        Assert.That(PostTitle.CreateTruncated("   "), Is.Null);

    [Test]
    public void MarkdownText_EmptyIsALegitimateValue()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MarkdownText.Empty.IsEmpty, Is.True);
            Assert.That(MarkdownText.Empty.Value, Is.Empty);
            Assert.That(new MarkdownText("  ").IsEmpty, Is.True);
        });
    }

    [Test]
    public void MarkdownText_Constructor_RejectsAnOverLongBody() =>
        Assert.That(
            () => new MarkdownText(new string('a', MarkdownText.MaxLength + 1)),
            Throws.TypeOf<DomainValidationException>());

    [Test]
    public void MarkdownText_TryCreate_TruncatesRatherThanRejecting()
    {
        MarkdownText.TryCreate(new string('a', MarkdownText.MaxLength + 10), out MarkdownText body);

        Assert.That(body.Value, Has.Length.EqualTo(MarkdownText.MaxLength));
    }

    [Test]
    public void ToPreview_FlattensToOneLineAndDropsSyntax()
    {
        var body = new MarkdownText("# Heading\n\nSome **bold** and _italic_ text with a [link](https://x.com).");

        Assert.That(body.ToPreview(200), Is.EqualTo("Heading Some bold and italic text with a link."));
    }

    [Test]
    public void ToPreview_AddsAnEllipsisWhenItRunsOut()
    {
        var body = new MarkdownText("The quick brown fox jumps over the lazy dog");

        string preview = body.ToPreview(20);

        Assert.Multiple(() =>
        {
            Assert.That(preview, Does.EndWith("…"));
            Assert.That(preview, Has.Length.LessThanOrEqualTo(21));
        });
    }

    /// <summary>
    /// Square brackets that are not a link are the author's own text. Stripping the opening one and
    /// leaving the closing one — which is what a naive filter does — reads as a typo.
    /// </summary>
    [Test]
    public void ToPreview_LeavesBracketsThatAreNotALinkAlone() =>
        Assert.That(
            new MarkdownText("[Deleted] Thanks, that worked!").ToPreview(100),
            Is.EqualTo("[Deleted] Thanks, that worked!"));

    [Test]
    public void ToPreview_KeepsALinksLabelAndDropsItsTarget() =>
        Assert.That(
            new MarkdownText("try [this forum post](https://example.com/a/b) instead").ToPreview(100),
            Is.EqualTo("try this forum post instead"));

    /// <summary>
    /// Community sidebars are full of rules lists fenced with "---" and Lemmy's ":::" spoiler
    /// blocks. Flattened onto the one line a directory row has, those read as line noise.
    /// </summary>
    [TestCase("Tech news. --- Our Rules --- 1. Be nice", "Tech news. Our Rules 1. Be nice")]
    [TestCase("Rules ::: spoiler 1. Be civil", "Rules spoiler 1. Be civil")]
    [TestCase("___ heading ___", "heading")]
    public void ToPreview_DropsRuleAndSpoilerFences(string body, string expected) =>
        Assert.That(new MarkdownText(body).ToPreview(100), Is.EqualTo(expected));

    /// <summary>A hyphen inside a word is not a fence and has to survive.</summary>
    [TestCase("a well-known re-entrant problem", "a well-known re-entrant problem")]
    [TestCase("ratio 16:9 at 10:30", "ratio 16:9 at 10:30")]
    public void ToPreview_LeavesOrdinaryPunctuationAlone(string body, string expected) =>
        Assert.That(new MarkdownText(body).ToPreview(100), Is.EqualTo(expected));

    /// <summary>
    /// Observed leaking into a feed row on a real instance. An image is not text; in a one-line
    /// preview its URL is pure noise and its alt text, if any, is the only readable part.
    /// </summary>
    [TestCase("![](https://substackcdn.com/image/fetch/$s_!Xoqx!,f_auto/https%3A%2F%2Fpost-media)", "")]
    [TestCase("Look: ![a chart](https://example.com/c.png) and more", "Look: a chart and more")]
    [TestCase("![](https://example.com/a.png) trailing text", "trailing text")]
    public void ToPreview_DropsInlineImages(string body, string expected) =>
        Assert.That(new MarkdownText(body).ToPreview(200), Is.EqualTo(expected));

    [Test]
    public void ToPreview_OfAnEmptyBodyIsEmpty() =>
        Assert.That(MarkdownText.Empty.ToPreview(50), Is.Empty);

    [Test]
    public void ToPreview_RejectsANonPositiveLength() =>
        Assert.That(() => new MarkdownText("x").ToPreview(0), Throws.TypeOf<ArgumentOutOfRangeException>());
}
