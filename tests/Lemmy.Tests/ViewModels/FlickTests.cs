using Avalonia;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// Which way a flick across the full-screen picture moves the gallery. Vertical, matching the feed
/// the pictures came from; the horizontal axis is left for the pictures of a single post.
/// </summary>
[TestFixture]
internal sealed class FlickTests
{
    private const double Far = Flick.Threshold + 10;
    private const double Short = Flick.Threshold - 10;

    [Test]
    public void FlickingUpBringsTheNextPictureTheWayItBringsTheNextPost() =>
        Assert.That(Flick.Read(new Vector(0, -Far)), Is.EqualTo(GalleryStep.Next));

    [Test]
    public void FlickingDownGoesBack() =>
        Assert.That(Flick.Read(new Vector(0, Far)), Is.EqualTo(GalleryStep.Previous));

    [Test]
    public void SidewaysMeansNothingYet() =>
        Assert.That(
            Flick.Read(new Vector(-Far, 0)),
            Is.EqualTo(GalleryStep.None),
            "the horizontal axis belongs to a post's own pictures");

    [Test]
    public void ADiagonalGoesWhicheverWayItWentFurthest()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Flick.Read(new Vector(Short, -Far)), Is.EqualTo(GalleryStep.Next));
            Assert.That(Flick.Read(new Vector(-Far, -Far - 1)), Is.EqualTo(GalleryStep.Next));
            Assert.That(Flick.Read(new Vector(-Far - 1, -Far)), Is.EqualTo(GalleryStep.None), "more sideways than up");
        });
    }

    [Test]
    public void AnExactlyDiagonalDragIsRefusedRatherThanGuessedAt() =>
        Assert.That(Flick.Read(new Vector(Far, Far)), Is.EqualTo(GalleryStep.None));

    [Test]
    public void ATapThatWanderedIsStillATap()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Flick.Read(default), Is.EqualTo(GalleryStep.None));
            Assert.That(Flick.Read(new Vector(0, -Short)), Is.EqualTo(GalleryStep.None));
            Assert.That(Flick.Read(new Vector(0, Short)), Is.EqualTo(GalleryStep.None));
        });
    }

    [Test]
    public void TheThresholdItselfCounts() =>
        Assert.That(Flick.Read(new Vector(0, -Flick.Threshold)), Is.EqualTo(GalleryStep.Next));
}
