using System.Collections.Immutable;
using Lemmy.Domain;

namespace Lemmy.Tests.Domain;

[TestFixture]
internal sealed class CommentPathTests
{
    [TestCase("0", 0)]
    [TestCase("0.25414623", 1)]
    [TestCase("0.25414623.25414700", 2)]
    [TestCase("0.1.2.3.4", 4)]
    public void Depth_CountsTheSeparatorsNotTheSyntheticRoot(string path, int expected) =>
        Assert.That(new CommentPath(path).Depth, Is.EqualTo(expected));

    [Test]
    public void IsTopLevel_IsTrueForADirectReplyToThePost()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new CommentPath("0.100").IsTopLevel, Is.True);
            Assert.That(new CommentPath("0.100.200").IsTopLevel, Is.False);
        });
    }

    [Test]
    public void ParentId_IsNullAtTheTopLevel() =>
        Assert.That(new CommentPath("0.100").ParentId, Is.Null);

    [Test]
    public void ParentId_IsTheSecondToLastSegment() =>
        Assert.That(new CommentPath("0.100.200.300").ParentId, Is.EqualTo(new CommentId(200)));

    [Test]
    public void RootId_IsTheFirstRealSegment()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new CommentPath("0.100.200.300").RootId, Is.EqualTo(new CommentId(100)));
            Assert.That(new CommentPath("0.100").RootId, Is.EqualTo(new CommentId(100)));
            Assert.That(new CommentPath("0").RootId, Is.Null);
        });
    }

    [Test]
    public void Ancestors_ExcludeTheSyntheticRootAndTheCommentItself()
    {
        ImmutableArray<CommentId> ancestors = new CommentPath("0.100.200.300").Ancestors;

        Assert.That(ancestors, Is.EqualTo(new[] { new CommentId(100), new CommentId(200) }));
    }

    [Test]
    public void Ancestors_AreEmptyAtTheTopLevel() =>
        Assert.That(new CommentPath("0.100").Ancestors, Is.Empty);

    [TestCase("")]
    [TestCase("100")]
    [TestCase("1.100")]
    [TestCase("0.")]
    [TestCase("0..100")]
    [TestCase("0.abc")]
    [TestCase("0.100.")]
    public void TryParse_RejectsAnythingThatIsNotARootedNumericChain(string path) =>
        Assert.That(CommentPath.TryParse(path, out _), Is.False);

    [Test]
    public void Append_BuildsThePathAReplyWouldHave() =>
        Assert.That(new CommentPath("0.100").Append(new CommentId(200)).Value, Is.EqualTo("0.100.200"));

    [Test]
    public void Default_IsNotValid()
    {
        Assert.Multiple(() =>
        {
            Assert.That(default(CommentPath).IsValid, Is.False);
            Assert.That(default(CommentPath).Depth, Is.Zero);
            Assert.That(default(CommentPath).ParentId, Is.Null);
        });
    }
}
