using System.Collections.Immutable;
using Avalonia.Media.Imaging;

namespace Lemmy.Services;

/// <summary>One frame of an animation, and how long it stays on screen.</summary>
/// <param name="Image">The decoded frame.</param>
/// <param name="Duration">How long to show it before moving on.</param>
public sealed record AnimationFrame(Bitmap Image, TimeSpan Duration);

/// <summary>
/// A decoded picture, which may have more than one frame. A still image is simply an animation of
/// one frame, so everything downstream — the viewer, its zoom, its gallery — handles both without
/// knowing the difference.
/// </summary>
public sealed class AnimatedImage : IDisposable
{
    /// <summary>Frames Skia reports as zero-length play at this rate, which is what browsers do.</summary>
    internal static readonly TimeSpan DefaultFrameDuration = TimeSpan.FromMilliseconds(100);

    /// <summary>Frames are decoded to premultiplied BGRA.</summary>
    private const int BytesPerPixel = 4;

    private bool isDisposed;

    /// <summary>Wraps decoded frames.</summary>
    /// <exception cref="ArgumentException"><paramref name="frames"/> is empty.</exception>
    public AnimatedImage(ImmutableArray<AnimationFrame> frames)
    {
        if (frames.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A picture needs at least one frame.", nameof(frames));
        }

        Frames = frames;

        TimeSpan total = TimeSpan.Zero;
        foreach (AnimationFrame frame in frames)
        {
            total += frame.Duration;
        }

        TotalDuration = total;

        long bytes = 0;
        foreach (AnimationFrame frame in frames)
        {
            bytes += (long)frame.Image.PixelSize.Width * frame.Image.PixelSize.Height * BytesPerPixel;
        }

        EstimatedBytes = bytes;
    }

    /// <summary>The frames, in order.</summary>
    public ImmutableArray<AnimationFrame> Frames { get; }

    /// <summary>How long one pass through the animation takes.</summary>
    public TimeSpan TotalDuration { get; }

    /// <summary>
    /// Roughly how much memory the decoded frames occupy. Approximate on purpose — it is used to
    /// decide how much to keep around, not to account for anything.
    /// </summary>
    public long EstimatedBytes { get; }

    /// <summary>Whether there is anything to animate.</summary>
    public bool IsAnimated => Frames.Length > 1;

    /// <summary>The first frame, which is what a still picture is.</summary>
    public Bitmap FirstFrame => Frames[0].Image;

    /// <summary>
    /// Which frame is showing <paramref name="elapsed"/> after the animation started, looping
    /// forever. Time is mapped to a frame rather than a frame being advanced per tick, so a slow
    /// device drops frames instead of playing the whole thing in slow motion.
    /// </summary>
    public int FrameIndexAt(TimeSpan elapsed)
    {
        if (Frames.Length == 1 || TotalDuration <= TimeSpan.Zero)
        {
            return 0;
        }

        TimeSpan position = elapsed <= TimeSpan.Zero
            ? TimeSpan.Zero
            : TimeSpan.FromTicks(elapsed.Ticks % TotalDuration.Ticks);

        TimeSpan running = TimeSpan.Zero;
        for (int index = 0; index < Frames.Length; index++)
        {
            running += Frames[index].Duration;
            if (position < running)
            {
                return index;
            }
        }

        return Frames.Length - 1;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        foreach (AnimationFrame frame in Frames)
        {
            frame.Image.Dispose();
        }
    }
}
