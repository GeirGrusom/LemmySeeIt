using Lemmy.Domain;

namespace Lemmy.Tests.Domain;

/// <summary>
/// A draft models something the reader typed, so unlike <see cref="MarkdownText"/> it is only valid
/// when there is something in it.
/// </summary>
[TestFixture]
internal sealed class CommentDraftTests
{
    [Test]
    public void AnOrdinaryCommentIsAccepted()
    {
        Assert.That(CommentDraft.TryCreate("Good point, though.".AsSpan(), out CommentDraft draft), Is.True);
        Assert.That(draft.Value, Is.EqualTo("Good point, though."));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\n\n\t ")]
    public void ABlankCommentIsNotAComment(string text)
    {
        Assert.Multiple(() =>
        {
            Assert.That(CommentDraft.TryCreate(text.AsSpan(), out _), Is.False);
            Assert.That(CommentDraft.Explain(text.AsSpan()), Does.Contain("needs something in it"));
        });
    }

    [Test]
    public void SurroundingWhitespaceIsNotPartOfWhatWasWritten()
    {
        _ = CommentDraft.TryCreate("  spaced out \n".AsSpan(), out CommentDraft draft);

        Assert.That(draft.Value, Is.EqualTo("spaced out"));
    }

    [Test]
    public void MarkdownIsLeftExactlyAsTyped()
    {
        const string Written = "**bold**, a [link](https://example.com) and\n\n> a quote";
        _ = CommentDraft.TryCreate(Written.AsSpan(), out CommentDraft draft);

        Assert.That(draft.Value, Is.EqualTo(Written));
    }

    [Test]
    public void AnOverLongCommentIsRejectedWithItsLength()
    {
        string tooLong = new('x', CommentDraft.MaxLength + 1);

        Assert.Multiple(() =>
        {
            Assert.That(CommentDraft.TryCreate(tooLong.AsSpan(), out _), Is.False);
            Assert.That(CommentDraft.Explain(tooLong.AsSpan()), Does.Contain("10001"));
            Assert.That(() => new CommentDraft(tooLong), Throws.TypeOf<DomainValidationException>());
        });
    }

    [Test]
    public void ACommentExactlyAtTheLimitIsFine() =>
        Assert.That(CommentDraft.TryCreate(new string('x', CommentDraft.MaxLength).AsSpan(), out _), Is.True);

    [Test]
    public void ADefaultDraftIsNotValid() =>
        Assert.That(default(CommentDraft).IsValid, Is.False);
}
