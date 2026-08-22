using Avalonia;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// The arithmetic a pinch drives. Both bugs it had were in how the gesture was read rather than in
/// this geometry, so these pin down what the geometry promises: a factor is relative, and the point
/// pinched about stays put.
/// </summary>
[TestFixture]
internal sealed class PinchZoomTests
{
    private static readonly Size Viewport = new(1000, 2000);
    private static readonly Size Fitted = new(1000, 1000);

    [Test]
    public void AFactorIsRelativeToWhereTheZoomAlreadyIs()
    {
        ZoomState once = ZoomState.Fitted.ScaledBy(2, new Point(500, 1000), Viewport, Fitted);
        ZoomState twice = once.ScaledBy(2, new Point(500, 1000), Viewport, Fitted);

        Assert.Multiple(() =>
        {
            Assert.That(once.Scale, Is.EqualTo(2).Within(0.001));
            Assert.That(twice.Scale, Is.EqualTo(4).Within(0.001));
        });
    }

    /// <summary>
    /// A pinch reports its scale as the distance between the fingers over their distance when the
    /// gesture began, so it grows as one continuous number. Feeding those numbers in as relative
    /// factors compounds them — 1.2, then 1.4, then 1.6 became 1.2 × 1.4 × 1.6 — which is why the
    /// picture used to slam to maximum on the first pinch. The view now divides them into deltas.
    /// </summary>
    [Test]
    public void CumulativeGestureScalesBecomeDeltas()
    {
        double[] reported = [1.2, 1.4, 1.6];

        ZoomState compounded = ZoomState.Fitted;
        foreach (double scale in reported)
        {
            compounded = compounded.ScaledBy(scale, new Point(500, 1000), Viewport, Fitted);
        }

        ZoomState stepped = ZoomState.Fitted;
        double previous = 1;
        foreach (double scale in reported)
        {
            stepped = stepped.ScaledBy(scale / previous, new Point(500, 1000), Viewport, Fitted);
            previous = scale;
        }

        Assert.Multiple(() =>
        {
            Assert.That(compounded.Scale, Is.EqualTo(2.688).Within(0.001), "what the bug did");
            Assert.That(stepped.Scale, Is.EqualTo(1.6).Within(0.001), "what the gesture asked for");
        });
    }

    [Test]
    public void ZoomingAboutTheCentreKeepsThePictureCentred()
    {
        ZoomState zoomed = ZoomState.Fitted.ScaledBy(2, new Point(500, 1000), Viewport, Fitted);

        Assert.That(zoomed.Offset, Is.EqualTo(default(Vector)));
    }

    /// <summary>
    /// The origin arrives in pixels. It used to be multiplied by the viewport size as though it
    /// were a fraction, which threw it thousands of pixels past the corner — and the clamp then
    /// pinned the picture to the bottom right, which is exactly what it looked like.
    /// </summary>
    [Test]
    public void AnOriginMistakenForAFractionPinsThePictureToACorner()
    {
        var origin = new Point(500, 1000);
        var asIfFraction = new Point(origin.X * Viewport.Width, origin.Y * Viewport.Height);

        ZoomState correct = ZoomState.Fitted.ScaledBy(4, origin, Viewport, Fitted);
        ZoomState wrong = ZoomState.Fitted.ScaledBy(4, asIfFraction, Viewport, Fitted);

        // Pushed hard against the clamp in both axes. A negative offset slides the picture up and
        // left, which leaves its bottom-right corner filling the screen — the reported symptom.
        double limitX = ((Fitted.Width * wrong.Scale) - Viewport.Width) / 2;
        double limitY = ((Fitted.Height * wrong.Scale) - Viewport.Height) / 2;

        Assert.Multiple(() =>
        {
            Assert.That(correct.Offset, Is.EqualTo(default(Vector)), "the centre stays the centre");
            Assert.That(wrong.Offset.X, Is.EqualTo(-limitX).Within(0.001));
            Assert.That(wrong.Offset.Y, Is.EqualTo(-limitY).Within(0.001));
        });
    }

    [Test]
    public void WhateverIsUnderTheFingersStaysUnderThem()
    {
        // A square viewport, so zooming leaves room to move in both axes and the edge clamp does
        // not bind. Where it does bind, staying at the edge rightly beats staying under the finger.
        var square = new Size(1000, 1000);
        var origin = new Point(250, 300);

        ZoomState zoomed = ZoomState.Fitted.ScaledBy(2, origin, square, Fitted);

        Vector before = ToImage(ZoomState.Fitted, origin, square);
        Vector after = ToImage(zoomed, origin, square);

        Assert.Multiple(() =>
        {
            Assert.That(zoomed.Offset, Is.Not.EqualTo(default(Vector)), "an off-centre pinch moves it");
            Assert.That((after - before).Length, Is.LessThan(0.001));
        });
    }

    [Test]
    public void TheEdgeClampWinsWhenThePictureCannotMoveThatWay()
    {
        // Fitted is 1000 tall in a 2000 tall viewport: at 2x it exactly fills, so it cannot slide
        // vertically at all and must stay centred however the pinch was aimed.
        ZoomState zoomed = ZoomState.Fitted.ScaledBy(2, new Point(250, 700), Viewport, Fitted);

        Assert.That(zoomed.Offset.Y, Is.EqualTo(0).Within(0.001));
    }

    [Test]
    public void ZoomingOutNeverGoesBelowFitting()
    {
        ZoomState out1 = ZoomState.Fitted.ScaledBy(0.2, new Point(500, 1000), Viewport, Fitted);

        Assert.Multiple(() =>
        {
            Assert.That(out1.Scale, Is.EqualTo(ZoomState.MinimumScale));
            Assert.That(out1.IsZoomed, Is.False);
        });
    }

    [Test]
    public void ZoomingInStopsAtTheMaximum()
    {
        ZoomState huge = ZoomState.Fitted.ScaledBy(100, new Point(500, 1000), Viewport, Fitted);

        Assert.That(huge.Scale, Is.EqualTo(ZoomState.MaximumScale));
    }

    /// <summary>Where a viewport point lands in the unscaled image, relative to its centre.</summary>
    private static Vector ToImage(ZoomState state, Point viewportPoint, Size? viewport = null)
    {
        Size size = viewport ?? Viewport;
        var fromCentre = new Vector(viewportPoint.X - (size.Width / 2), viewportPoint.Y - (size.Height / 2));
        return (fromCentre - state.Offset) / state.Scale;
    }
}
