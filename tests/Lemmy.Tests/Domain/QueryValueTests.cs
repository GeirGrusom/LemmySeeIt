using Lemmy.Domain;

namespace Lemmy.Tests.Domain;

[TestFixture]
internal sealed class QueryValueTests
{
    [Test]
    public void PageSize_DefaultResolvesToTheAppsOwnDefault() =>
        Assert.That(PageSize.Default.Value, Is.EqualTo(20));

    [TestCase(0)]
    [TestCase(51)]
    [TestCase(-1)]
    public void PageSize_RejectsAnythingAServerWould(int value) =>
        Assert.That(() => new PageSize(value), Throws.TypeOf<DomainValidationException>());

    [TestCase(0, PageSize.Minimum)]
    [TestCase(500, PageSize.Maximum)]
    [TestCase(25, 25)]
    public void PageSize_Clamp_BringsAnythingIntoRange(int value, int expected) =>
        Assert.That(PageSize.Clamp(value).Value, Is.EqualTo(expected));

    [Test]
    public void CommentDepth_DefaultResolvesToTheAppsOwnDefault() =>
        Assert.That(CommentDepth.Default.Value, Is.EqualTo(8));

    [Test]
    public void CommentDepth_Clamp_BringsAnythingIntoRange() =>
        Assert.That(CommentDepth.Clamp(999).Value, Is.EqualTo(CommentDepth.Maximum));

    [TestCase("linux gaming", "linux gaming")]
    [TestCase("  spaced  ", "spaced")]
    public void SearchTerm_TrimsWhatWasTyped(string input, string expected)
    {
        SearchTerm.TryCreate(input, out SearchTerm term);

        Assert.That(term.Value, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("a")]
    public void SearchTerm_RejectsWhatWouldMatchEverything(string input) =>
        Assert.That(SearchTerm.TryCreate(input, out _), Is.False);

    [Test]
    public void SearchTerm_RejectsAnOverLongTerm() =>
        Assert.That(SearchTerm.TryCreate(new string('a', SearchTerm.MaxLength + 1), out _), Is.False);

    [Test]
    public void PageCursor_KeepsTheServersTokenVerbatim()
    {
        PageCursor.TryCreate("P308c595", out PageCursor cursor);

        Assert.That(cursor.Value, Is.EqualTo("P308c595"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("has space")]
    public void PageCursor_RejectsAnythingUnusable(string input) =>
        Assert.That(PageCursor.TryCreate(input, out _), Is.False);
}
