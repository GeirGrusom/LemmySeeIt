using Avalonia;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// The geometry a reader feels but never sees. A 400x800 viewport showing an image fitted to
/// 400x600 is the running example, so the picture is letterboxed vertically at rest.
/// </summary>
[TestFixture]
internal sealed class ZoomStateTests
{
    private static readonly Size Viewport = new(400, 800);
    private static readonly Size Fitted = new(400, 600);
    private static readonly Point Centre = new(200, 400);

    [Test]
    public void FittedIsNotZoomedAndSitsCentred()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ZoomState.Fitted.Scale, Is.EqualTo(ZoomState.MinimumScale));
            Assert.That(ZoomState.Fitted.Offset, Is.EqualTo(default(Vector)));
            Assert.That(ZoomState.Fitted.IsZoomed, Is.False);
        });
    }

    [Test]
    public void ScalingInZoomsAndReportsItself()
    {
        ZoomState zoomed = ZoomState.Fitted.ScaledBy(2, Centre, Viewport, Fitted);

        Assert.Multiple(() =>
        {
            Assert.That(zoomed.Scale, Is.EqualTo(2).Within(0.001));
            Assert.That(zoomed.IsZoomed, Is.True);
        });
    }

    [Test]
    public void ZoomNeverGoesBelowFittingTheScreen()
    {
        ZoomState state = ZoomState.Fitted.ScaledBy(0.25, Centre, Viewport, Fitted);

        Assert.That(state.Scale, Is.EqualTo(ZoomState.MinimumScale));
    }

    [Test]
    public void ZoomStopsAtTheMaximum()
    {
        ZoomState state = ZoomState.Fitted.ScaledBy(100, Centre, Viewport, Fitted);

        Assert.That(state.Scale, Is.EqualTo(ZoomState.MaximumScale));
    }

    /// <summary>
    /// The point under the fingers has to stay under the fingers, or a pinch feels like it is
    /// fighting you. Zooming about the centre must therefore not shift the image at all.
    /// </summary>
    [Test]
    public void ZoomingAboutTheCentreDoesNotShiftTheImage()
    {
        ZoomState state = ZoomState.Fitted.ScaledBy(3, Centre, Viewport, Fitted);

        Assert.That(state.Offset, Is.EqualTo(default(Vector)));
    }

    /// <summary>
    /// Zooming about a point left of centre pushes the image right, so that what was under the
    /// point stays there.
    /// </summary>
    [Test]
    public void ZoomingAboutAnOffCentrePointMovesTheImageToKeepItUnderTheFinger()
    {
        ZoomState state = ZoomState.Fitted.ScaledBy(2, new Point(100, 400), Viewport, Fitted);

        Assert.That(state.Offset.X, Is.GreaterThan(0));
    }

    [Test]
    public void PanningIsIgnoredWhileTheWholeImageAlreadyFits()
    {
        ZoomState state = ZoomState.Fitted.PannedBy(new Vector(120, 90), Viewport, Fitted);

        Assert.That(state.Offset, Is.EqualTo(default(Vector)));
    }

    /// <summary>
    /// At 2x the image is 800x1200 against a 400x800 viewport, so there is 200 of slack each way
    /// horizontally and 200 vertically. Dragging further must stop at the edge rather than pull the
    /// picture off into empty space.
    /// </summary>
    [Test]
    public void PanningStopsAtTheEdgesOfTheZoomedImage()
    {
        ZoomState zoomed = ZoomState.Fitted.ScaledBy(2, Centre, Viewport, Fitted);

        ZoomState panned = zoomed.PannedBy(new Vector(1000, 1000), Viewport, Fitted);

        Assert.Multiple(() =>
        {
            Assert.That(panned.Offset.X, Is.EqualTo(200).Within(0.001));
            Assert.That(panned.Offset.Y, Is.EqualTo(200).Within(0.001));
        });
    }

    [Test]
    public void PanningWithinTheEdgesIsHonoured()
    {
        ZoomState zoomed = ZoomState.Fitted.ScaledBy(2, Centre, Viewport, Fitted);

        ZoomState panned = zoomed.PannedBy(new Vector(50, -30), Viewport, Fitted);

        Assert.Multiple(() =>
        {
            Assert.That(panned.Offset.X, Is.EqualTo(50).Within(0.001));
            Assert.That(panned.Offset.Y, Is.EqualTo(-30).Within(0.001));
        });
    }

    [Test]
    public void DoubleTapZoomsInFromFitted()
    {
        ZoomState state = ZoomState.Fitted.ToggledAt(Centre, Viewport, Fitted);

        Assert.That(state.Scale, Is.EqualTo(ZoomState.DoubleTapScale).Within(0.001));
    }

    [Test]
    public void DoubleTapZoomsBackOutWhenAlreadyZoomed()
    {
        ZoomState zoomed = ZoomState.Fitted.ScaledBy(4, Centre, Viewport, Fitted);

        Assert.That(zoomed.ToggledAt(Centre, Viewport, Fitted), Is.EqualTo(ZoomState.Fitted));
    }

    /// <summary>
    /// Rotating the phone shrinks the viewport under a pan that was legal a moment ago; the image
    /// has to be pulled back rather than left hanging off the edge.
    /// </summary>
    [Test]
    public void ClampingPullsAnOffsetBackWhenTheViewportShrinks()
    {
        var panned = new ZoomState(2, new Vector(200, 200));

        ZoomState clamped = panned.Clamped(new Size(800, 800), Fitted);

        Assert.That(clamped.Offset.X, Is.Zero, "at 2x the image is 800 wide, exactly filling the viewport");
    }

    [Test]
    public void AZeroSizedViewportDoesNotProduceNonsense()
    {
        ZoomState state = ZoomState.Fitted.ScaledBy(2, default, default, default);

        Assert.Multiple(() =>
        {
            Assert.That(state.Scale, Is.EqualTo(2).Within(0.001));
            Assert.That(state.Offset, Is.EqualTo(default(Vector)));
        });
    }
}
