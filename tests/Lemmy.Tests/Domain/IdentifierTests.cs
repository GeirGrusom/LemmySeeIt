using Lemmy.Domain;

namespace Lemmy.Tests.Domain;

[TestFixture]
internal sealed class IdentifierTests
{
    [Test]
    public void Constructor_KeepsAPositiveValue()
    {
        var id = new PostId(50908658);

        Assert.Multiple(() =>
        {
            Assert.That(id.Value, Is.EqualTo(50908658));
            Assert.That(id.IsValid, Is.True);
            Assert.That(id.ToString(), Is.EqualTo("50908658"));
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void Constructor_RejectsAnythingButAPositiveValue(int value) =>
        Assert.That(() => new PostId(value), Throws.TypeOf<DomainValidationException>());

    [Test]
    public void Default_IsNotValid() => Assert.That(default(PostId).IsValid, Is.False);

    [Test]
    public void TryCreate_ReportsFailureWithoutThrowing()
    {
        bool created = PostId.TryCreate(0, out PostId id);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(id, Is.EqualTo(default(PostId)));
        });
    }

    [TestCase("42", 42)]
    [TestCase("2147483647", int.MaxValue)]
    public void TryParse_ReadsDecimalText(string text, int expected)
    {
        bool parsed = CommentId.TryParse(text, out CommentId id);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(id.Value, Is.EqualTo(expected));
        });
    }

    [TestCase("")]
    [TestCase("0")]
    [TestCase("-3")]
    [TestCase("4.2")]
    [TestCase("2147483648")]
    [TestCase(" 42")]
    [TestCase("42abc")]
    public void TryParse_RejectsAnythingElse(string text) =>
        Assert.That(CommentId.TryParse(text, out _), Is.False);

    [Test]
    public void TryFormat_WritesIntoASpanWithoutAllocating()
    {
        Span<char> buffer = stackalloc char[16];

        bool formatted = new CommunityId(2478).TryFormat(buffer, out int written, default, null);
        string text = buffer[..written].ToString();

        Assert.Multiple(() =>
        {
            Assert.That(formatted, Is.True);
            Assert.That(text, Is.EqualTo("2478"));
        });
    }

    [Test]
    public void TryFormat_ReportsFailureWhenTheBufferIsTooSmall()
    {
        Span<char> buffer = stackalloc char[2];

        Assert.That(new PersonId(306405).TryFormat(buffer, out _, default, null), Is.False);
    }

    [Test]
    public void Equality_ComparesByValue()
    {
        var seven = new PostId(7);

        Assert.Multiple(() =>
        {
            Assert.That(seven, Is.EqualTo(new PostId(7)));
            Assert.That(seven, Is.Not.EqualTo(new PostId(8)));
        });
    }

    /// <summary>
    /// The whole point of separate identifier types: a post id and a comment id with the same
    /// number are different things, and the compiler should be the one enforcing that.
    /// </summary>
    [Test]
    public void DifferentIdentifierTypes_AreNotInterchangeable()
    {
        object postId = new PostId(7);
        object commentId = new CommentId(7);

        Assert.That(postId, Is.Not.EqualTo(commentId));
    }

    [Test]
    public void LanguageId_AllowsZeroBecauseLemmyReservesItForUndetermined()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LanguageId.Undetermined.IsUndetermined, Is.True);
            Assert.That(new LanguageId(0).Value, Is.Zero);
            Assert.That(new LanguageId(37).IsUndetermined, Is.False);
            Assert.That(() => new LanguageId(-1), Throws.TypeOf<DomainValidationException>());
        });
    }
}
