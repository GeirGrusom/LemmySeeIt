using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Tests.TestSupport;

namespace Lemmy.Tests.Domain;

[TestFixture]
internal sealed class ImagePostTests
{
    /// <summary>
    /// The reported media type is authoritative. Instances serve images through proxy endpoints
    /// whose URLs carry no file extension at all, and those are exactly the posts an art community
    /// is full of.
    /// </summary>
    [Test]
    public void APostWithAnExtensionlessImageUrlIsStillAnImage()
    {
        Post post = Sample.Post(
            url: "https://slrpnk.net/api/v3/image_proxy?url=https%3A%2F%2Fexample.com%2Fa",
            contentType: "image/png");

        Assert.Multiple(() =>
        {
            Assert.That(post.IsImage, Is.True);
            Assert.That(post.FullImage?.Value, Is.EqualTo(post.Url?.Value));
        });
    }

    /// <summary>An article with a picture attached is not a picture.</summary>
    [Test]
    public void AnArticleIsNotAnImageEvenThoughItHasAThumbnail()
    {
        Post post = Sample.Post(url: "https://apnews.com/article/x", contentType: "text/html; charset=utf-8");

        Assert.Multiple(() =>
        {
            Assert.That(post.IsImage, Is.False);
            Assert.That(post.FullImage, Is.Null);
        });
    }

    /// <summary>Older instances omit the media type; the extension is the fallback, not the rule.</summary>
    [Test]
    public void WithoutAMediaTypeTheExtensionDecides()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Sample.Post(url: "https://example.com/a.jpg").IsImage, Is.True);
            Assert.That(Sample.Post(url: "https://example.com/a.html").IsImage, Is.False);
        });
    }

    [Test]
    public void ASelfPostIsNotAnImage()
    {
        Post post = Sample.Post();

        Assert.Multiple(() =>
        {
            Assert.That(post.IsImage, Is.False);
            Assert.That(post.FullImage, Is.Null);
        });
    }

    /// <summary>
    /// The viewer wants the original, not the server's downscaled feed thumbnail — blowing that up
    /// full screen is exactly the disappointment this feature exists to avoid.
    /// </summary>
    [Test]
    public void FullImagePrefersTheOriginalOverTheThumbnail()
    {
        Post post = Sample.Post(url: "https://example.com/full.png", contentType: "image/png") with
        {
            Thumbnail = WebLink.Parse("https://example.com/thumb.png"),
        };

        Assert.Multiple(() =>
        {
            Assert.That(post.FullImage?.Value, Is.EqualTo("https://example.com/full.png"));
            Assert.That(post.PreviewImage?.Value, Is.EqualTo("https://example.com/thumb.png"));
        });
    }
}
