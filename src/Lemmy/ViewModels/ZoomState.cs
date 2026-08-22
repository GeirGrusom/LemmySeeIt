using Avalonia;

namespace Lemmy.ViewModels;

/// <summary>
/// How far an image is zoomed and how it is panned. Pure geometry, deliberately kept out of the
/// view: for comics and detailed art this is the part that has to be right, and it is far easier to
/// get right when it can be tested without a finger on a screen.
/// </summary>
/// <param name="Scale">Multiplier over the size the image is shown at when it first fits the screen.</param>
/// <param name="Offset">
/// Translation from centred, in screen pixels. Always within what the zoomed image can actually
/// cover, so the reader can never drag a picture off into empty space.
/// </param>
public readonly record struct ZoomState(double Scale, Vector Offset)
{
    /// <summary>Fully zoomed out: the whole image visible.</summary>
    public const double MinimumScale = 1.0;

    /// <summary>Enough to read the small print in a comic panel without turning pixels into soup.</summary>
    public const double MaximumScale = 8.0;

    /// <summary>What a double-tap zooms to when the image is not already zoomed.</summary>
    public const double DoubleTapScale = 2.5;

    private const double Epsilon = 0.001;

    /// <summary>The whole image, centred.</summary>
    public static ZoomState Fitted => new(MinimumScale, default);

    /// <summary>Whether the reader has zoomed in at all.</summary>
    public bool IsZoomed => Scale > MinimumScale + Epsilon;

    /// <summary>
    /// Zooms by <paramref name="factor"/> about <paramref name="origin"/>, keeping whatever is under
    /// that point where it is — which is what makes a pinch feel attached to the fingers doing it.
    /// </summary>
    /// <param name="factor">Relative change, e.g. 1.1 to zoom in a tenth.</param>
    /// <param name="origin">The point to zoom about, in viewport coordinates.</param>
    /// <param name="viewport">The size of the area the image is shown in.</param>
    /// <param name="fitted">The size the image occupies at <see cref="MinimumScale"/>.</param>
    public ZoomState ScaledBy(double factor, Point origin, Size viewport, Size fitted)
    {
        double target = Math.Clamp(Scale * factor, MinimumScale, MaximumScale);
        if (Math.Abs(target - Scale) < Epsilon)
        {
            return this;
        }

        // Relative to the centre, because that is what the render transform scales about.
        var fromCentre = new Vector(origin.X - (viewport.Width / 2), origin.Y - (viewport.Height / 2));
        Vector moved = fromCentre - ((fromCentre - Offset) * (target / Scale));

        return new ZoomState(target, moved).Clamped(viewport, fitted);
    }

    /// <summary>Drags the image by <paramref name="delta"/>, stopping at its edges.</summary>
    public ZoomState PannedBy(Vector delta, Size viewport, Size fitted) =>
        new ZoomState(Scale, Offset + delta).Clamped(viewport, fitted);

    /// <summary>
    /// What a double-tap does: zoom in on the tapped point, or go back to fitting the screen if
    /// already zoomed. One gesture, both directions — nobody wants to pinch out five times.
    /// </summary>
    public ZoomState ToggledAt(Point origin, Size viewport, Size fitted) =>
        IsZoomed
            ? Fitted
            : ScaledBy(DoubleTapScale / Scale, origin, viewport, fitted);

    /// <summary>
    /// Pulls the offset back into range. An axis where the zoomed image is smaller than the viewport
    /// is pinned to centred; otherwise the image may not be dragged past its own edge.
    /// </summary>
    public ZoomState Clamped(Size viewport, Size fitted)
    {
        double limitX = Math.Max(0, ((fitted.Width * Scale) - viewport.Width) / 2);
        double limitY = Math.Max(0, ((fitted.Height * Scale) - viewport.Height) / 2);

        return this with
        {
            Offset = new Vector(
                Math.Clamp(Offset.X, -limitX, limitX),
                Math.Clamp(Offset.Y, -limitY, limitY)),
        };
    }
}
