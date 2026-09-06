using Lemmy.Services;

namespace Lemmy.Tests.Services;

/// <summary>
/// Naming a picture format from its first bytes, which is only ever asked once decoding has already
/// failed. The point is to tell a reader that an AVIF will never work here, rather than leaving
/// them to retry a picture that cannot come.
/// </summary>
[TestFixture]
internal sealed class ImageSignatureTests
{
    /// <summary>An ISO base media header: four length bytes, "ftyp", then the brand.</summary>
    private static byte[] Container(string brand) =>
        [0x00, 0x00, 0x00, 0x1C, .. "ftyp"u8, .. System.Text.Encoding.ASCII.GetBytes(brand), 0, 0, 0, 0];

    [Test]
    public void AvifIsNamed() => Assert.That(ImageSignature.NameOf(Container("avif")), Is.EqualTo("AVIF"));

    [Test]
    public void AnAvifSequenceIsStillAnAvif() =>
        Assert.That(ImageSignature.NameOf(Container("avis")), Is.EqualTo("AVIF"));

    [TestCase("heic")]
    [TestCase("heix")]
    [TestCase("mif1")]
    public void HeifIsNamed(string brand) =>
        Assert.That(ImageSignature.NameOf(Container(brand)), Is.EqualTo("HEIF"));

    [Test]
    public void AnUnknownContainerBrandIsNotGuessedAt() =>
        Assert.That(ImageSignature.NameOf(Container("qt  ")), Is.Null);

    [Test]
    public void BareJpegXlIsNamed() =>
        Assert.That(ImageSignature.NameOf([0xFF, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]), Is.EqualTo("JPEG XL"));

    [Test]
    public void BoxedJpegXlIsNamed() =>
        Assert.That(
            ImageSignature.NameOf([0x00, 0x00, 0x00, 0x0C, .. "JXL "u8, 0x0D, 0x0A, 0x87, 0x0A]),
            Is.EqualTo("JPEG XL"));

    [Test]
    public void SvgIsNamedDespiteHavingNoMagicNumber() =>
        Assert.That(
            ImageSignature.NameOf("<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\" />"u8),
            Is.EqualTo("SVG"));

    /// <summary>
    /// The formats that do decode must come back unnamed. Naming one would turn a corrupt JPEG into
    /// "this app cannot show JPEGs", which is both wrong and alarming.
    /// </summary>
    [Test]
    public void AFormatThisAppCanActuallyReadIsNotNamed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ImageSignature.NameOf([0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0]), Is.Null, "JPEG");
            Assert.That(ImageSignature.NameOf([0x89, .. "PNG"u8, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]), Is.Null, "PNG");
            Assert.That(ImageSignature.NameOf([.. "GIF89a"u8, 0, 0, 0, 0, 0, 0]), Is.Null, "GIF");
            Assert.That(ImageSignature.NameOf([.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8]), Is.Null, "WebP");
        });
    }

    [Test]
    public void TooFewBytesToTellIsNotAGuess() =>
        Assert.That(ImageSignature.NameOf([0x00, 0x01, 0x02]), Is.Null);
}
