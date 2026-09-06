using Avalonia;

namespace Lemmy.ViewModels;

/// <summary>Where a flick across the picture wants to go.</summary>
public enum GalleryStep
{
    /// <summary>Nowhere: too short, or across the axis the gallery does not move on.</summary>
    None,

    /// <summary>The picture before this one.</summary>
    Previous,

    /// <summary>The picture after this one.</summary>
    Next,
}

/// <summary>
/// Reads a flick. Kept out of the view for the same reason <see cref="ZoomState"/> is: it is a
/// decision about direction and distance, and deciding it correctly should not require a finger on
/// a screen.
/// </summary>
public static class Flick
{
    /// <summary>How far a flick has to travel before it means "another picture" rather than "dismiss".</summary>
    public const double Threshold = 60;

    /// <summary>
    /// Works out what a completed drag meant.
    /// </summary>
    /// <remarks>
    /// The gallery moves on the vertical axis, because that is the axis the pictures were being
    /// read on: these are the posts of a feed, and a flick up brings the next one exactly as it
    /// does in the feed itself. It also leaves the horizontal axis free, which is where the several
    /// pictures of a single post belong once there are any.
    /// <para>
    /// A drag that is more sideways than up-and-down means nothing here, and neither does a short
    /// one — a picture is dismissed by a tap, and a tap that wandered a few pixels is still a tap.
    /// </para>
    /// </remarks>
    /// <param name="travelled">How far the drag moved in total, in viewport pixels.</param>
    public static GalleryStep Read(Vector travelled)
    {
        if (Math.Abs(travelled.Y) < Threshold || Math.Abs(travelled.Y) <= Math.Abs(travelled.X))
        {
            return GalleryStep.None;
        }

        return travelled.Y < 0 ? GalleryStep.Next : GalleryStep.Previous;
    }
}
