using Lemmy.Domain;

namespace Lemmy.Tests.Domain;

[TestFixture]
internal sealed class MediaTypeTests
{
    [TestCase("image/jpeg", "image/jpeg")]
    [TestCase("IMAGE/PNG", "image/png")]
    [TestCase("  image/webp  ", "image/webp")]
    [TestCase("text/html; charset=utf-8", "text/html")]
    [TestCase("application/vnd.api+json", "application/vnd.api+json")]
    public void TryParse_NormalisesAndDropsParameters(string input, string expected)
    {
        bool parsed = MediaType.TryParse(input, out MediaType mediaType);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(mediaType.Value, Is.EqualTo(expected));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("image")]
    [TestCase("/jpeg")]
    [TestCase("image/")]
    [TestCase("image/jpeg/extra")]
    [TestCase("image jpeg")]
    public void TryParse_RejectsAnythingThatIsNotATypeSubtypePair(string input) =>
        Assert.That(MediaType.TryParse(input, out _), Is.False);

    [TestCase("image/jpeg", true)]
    [TestCase("image/svg+xml", true)]
    [TestCase("text/html", false)]
    [TestCase("video/mp4", false)]
    public void IsImage_ReadsTheType(string input, bool expected)
    {
        MediaType.TryParse(input, out MediaType mediaType);

        Assert.That(mediaType.IsImage, Is.EqualTo(expected));
    }

    [Test]
    public void Kind_IsThePartBeforeTheSlash()
    {
        MediaType.TryParse("image/gif", out MediaType mediaType);

        Assert.Multiple(() =>
        {
            Assert.That(mediaType.Kind.ToString(), Is.EqualTo("image"));
            Assert.That(mediaType.IsAnimatedImage, Is.True);
        });
    }

    [Test]
    public void Default_IsNotValidAndIsNotAnImage()
    {
        Assert.Multiple(() =>
        {
            Assert.That(default(MediaType).IsValid, Is.False);
            Assert.That(default(MediaType).IsImage, Is.False);
        });
    }

    [Test]
    public void Constructor_ThrowsOnRejection() =>
        Assert.That(() => new MediaType("nonsense"), Throws.TypeOf<DomainValidationException>());
}
