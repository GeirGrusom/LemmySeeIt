using Lemmy.Domain;

namespace Lemmy.Tests.Domain;

[TestFixture]
internal sealed class WebLinkTests
{
    [TestCase("https://lemmy.world/post/1", "lemmy.world")]
    [TestCase("http://example.com", "example.com")]
    [TestCase("https://www.engadget.com/a/b", "engadget.com")]
    [TestCase("https://media.piefed.social/f/x.png?w=2", "media.piefed.social")]
    public void Host_DropsTheSchemeThePathAndALeadingWww(string url, string expected) =>
        Assert.That(WebLink.Parse(url).Host.ToString(), Is.EqualTo(expected));

    [TestCase("https://x.com/a.png", true)]
    [TestCase("https://x.com/a.JPEG", true)]
    [TestCase("https://x.com/a.webp?size=2", true)]
    [TestCase("https://x.com/a.html", false)]
    [TestCase("https://x.com/a", false)]
    public void LooksLikeImage_ReadsTheExtensionPastAnyQuery(string url, bool expected) =>
        Assert.That(WebLink.Parse(url).LooksLikeImage, Is.EqualTo(expected));

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("/relative/path")]
    [TestCase("ftp://example.com/x")]
    [TestCase("javascript:alert(1)")]
    [TestCase("example.com")]
    public void TryParse_RejectsAnythingThatIsNotAnAbsoluteHttpUrl(string url) =>
        Assert.That(WebLink.TryParse(url, out _), Is.False);

    [Test]
    public void ToUri_ThrowsOnADefaultLink() =>
        Assert.That(() => default(WebLink).ToUri(), Throws.TypeOf<InvalidOperationException>());

    [Test]
    public void ActorId_ExposesTheInstanceThatOwnsTheItem()
    {
        var actorId = new ActorId("https://lemmy.world/c/technology");

        Assert.That(actorId.Instance, Is.EqualTo(InstanceAddress.Parse("lemmy.world")));
    }

    [Test]
    public void ActorId_RejectsAnythingThatIsNotAnAbsoluteUrl() =>
        Assert.That(() => new ActorId("lemmy.world/c/technology"), Throws.TypeOf<DomainValidationException>());
}
