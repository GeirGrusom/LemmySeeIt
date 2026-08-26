using Lemmy.Domain;

namespace Lemmy.Tests.Domain;

/// <summary>What the three text boxes of the post form are allowed to contain.</summary>
[TestFixture]
internal sealed class PostDraftTests
{
    private static PostDraft Draft(string title = "A headline", string url = "", string body = "", bool isNsfw = false)
    {
        Assert.That(
            PostDraft.TryCreate(title.AsSpan(), url.AsSpan(), body.AsSpan(), isNsfw, out PostDraft draft),
            Is.True,
            PostDraft.Explain(title.AsSpan(), url.AsSpan(), body.AsSpan()));

        return draft;
    }

    [Test]
    public void ATitleIsTheOnlyThingAPostMustHave()
    {
        PostDraft draft = Draft();

        Assert.Multiple(() =>
        {
            Assert.That(draft.IsValid, Is.True);
            Assert.That(draft.HasUrl, Is.False);
            Assert.That(draft.HasBody, Is.False);
            Assert.That(draft.Body.IsEmpty, Is.True);
        });
    }

    [Test]
    public void ADefaultDraftIsNotValid() =>
        Assert.That(default(PostDraft).IsValid, Is.False);

    [Test]
    public void ABlankTitleIsRefusedAndExplained()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PostDraft.TryCreate("   ".AsSpan(), "".AsSpan(), "".AsSpan(), false, out _), Is.False);
            Assert.That(PostDraft.Explain("   ".AsSpan(), "".AsSpan(), "".AsSpan()), Does.Contain("blank"));
        });
    }

    [Test]
    public void AnOverLongTitleIsRefused()
    {
        string title = new('x', PostTitle.MaxLength + 1);

        Assert.Multiple(() =>
        {
            Assert.That(PostDraft.TryCreate(title.AsSpan(), "".AsSpan(), "".AsSpan(), false, out _), Is.False);
            Assert.That(PostDraft.Explain(title.AsSpan(), "".AsSpan(), "".AsSpan()), Does.Contain("200"));
        });
    }

    [Test]
    public void ALinkWithNoSchemeIsAssumedToBeHttps()
    {
        PostDraft draft = Draft(url: " example.com/article ");

        Assert.That(draft.Url?.Value, Is.EqualTo("https://example.com/article"));
    }

    [Test]
    public void ALinkThatAlreadyHasOneIsLeftAlone()
    {
        PostDraft draft = Draft(url: "http://example.com/article");

        Assert.That(draft.Url?.Value, Is.EqualTo("http://example.com/article"));
    }

    [Test]
    public void ASchemeThatIsNotTheWebIsRefusedRatherThanPrefixed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PostDraft.TryCreate("A headline".AsSpan(), "ftp://example.com".AsSpan(), "".AsSpan(), false, out _), Is.False);
            Assert.That(PostDraft.ExplainLink("ftp://example.com".AsSpan()), Is.Not.Null);
        });
    }

    [Test]
    public void AnEmptyLinkBoxIsNotAComplaint() =>
        Assert.That(PostDraft.ExplainLink("   ".AsSpan()), Is.Null, "a post does not need a link");

    [Test]
    public void TrailingWhitespaceIsNotPartOfTheBody()
    {
        PostDraft draft = Draft(body: "  Something to say.\n\n  ");

        Assert.Multiple(() =>
        {
            Assert.That(draft.Body.Value, Is.EqualTo("Something to say."));
            Assert.That(draft.HasBody, Is.True);
        });
    }

    [Test]
    public void AnOverLongBodyIsRefusedRatherThanClipped()
    {
        string body = new('x', PostDraft.MaxBodyLength + 1);

        Assert.Multiple(() =>
        {
            Assert.That(PostDraft.TryCreate("A headline".AsSpan(), "".AsSpan(), body.AsSpan(), false, out _), Is.False);
            Assert.That(
                PostDraft.Explain("A headline".AsSpan(), "".AsSpan(), body.AsSpan()),
                Does.Contain(PostDraft.MaxBodyLength.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        });
    }

    [Test]
    public void ABodyExactlyAtTheLimitIsAccepted()
    {
        PostDraft draft = Draft(body: new string('x', PostDraft.MaxBodyLength));

        Assert.That(draft.Body.Value, Has.Length.EqualTo(PostDraft.MaxBodyLength));
    }

    [Test]
    public void TheNotSafeForWorkFlagIsCarried() =>
        Assert.That(Draft(isNsfw: true).IsNsfw, Is.True);

    [Test]
    public void BuildingOneFromInvalidPartsThrows() =>
        Assert.That(
            () => new PostDraft(default, null, MarkdownText.Empty),
            Throws.TypeOf<DomainValidationException>());
}
