using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace Lemmy.Services;

/// <summary>
/// Decodes a picture, animating it when it has more than one frame. Avalonia's own bitmap loader
/// stops at the first frame, which is right for a feed row and wrong for a viewer full of reaction
/// GIFs.
/// </summary>
internal static class AnimatedImageDecoder
{
    /// <summary>
    /// How much decoded animation to hold at once. A long GIF at full size is easily hundreds of
    /// megabytes once every frame is a bitmap, and a phone will not survive that; past this the
    /// picture is shown as a still instead, which is what it did before animation existed.
    /// </summary>
    private const long FrameBudgetInBytes = 96L * 1024 * 1024;

    /// <summary>Beyond this a "GIF" is a video someone has mislabelled.</summary>
    private const int MaxFrames = 600;

    private static readonly Vector StandardDpi = new(96, 96);

    /// <summary>
    /// Decodes <paramref name="data"/>. Still pictures come back as a single frame scaled to
    /// <paramref name="decodeWidth"/>; animations come back at their own size, because scaling every
    /// frame costs more than the frames are worth. Returns <see langword="null"/> for anything
    /// undecodable.
    /// </summary>
    /// <remarks>
    /// Takes bytes rather than a stream deliberately. <c>SKCodec.Create(Stream)</c> takes ownership
    /// and closes what it is given, so a stream cannot be read twice — and this needs two looks at
    /// it: one to count the frames, one to decode them.
    /// </remarks>
    internal static AnimatedImage? Decode(byte[] data, int decodeWidth)
    {
        ArgumentNullException.ThrowIfNull(data);

        return CountFrames(data) > 1
            ? DecodeAnimation(data) ?? DecodeStill(data, decodeWidth)
            : DecodeStill(data, decodeWidth);
    }

    /// <summary>Reads just enough of the header to know whether this is worth decoding frame by frame.</summary>
    private static int CountFrames(byte[] data)
    {
        try
        {
            using SKData skiaData = SKData.CreateCopy(data);
            using SKCodec? codec = SKCodec.Create(skiaData);
            return codec?.FrameCount ?? 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return 0;
        }
    }

    /// <summary>Decodes the first frame only, scaled — what a feed row wants.</summary>
    internal static Bitmap? DecodeStillFrame(byte[] data, int decodeWidth)
    {
        try
        {
            using var stream = new MemoryStream(data, writable: false);
            return Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.MediumQuality);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
    }

    private static AnimatedImage? DecodeStill(byte[] data, int decodeWidth)
    {
        Bitmap? bitmap = DecodeStillFrame(data, decodeWidth);

        return bitmap is null ? null : new AnimatedImage([new AnimationFrame(bitmap, TimeSpan.Zero)]);
    }

    /// <summary>
    /// Decodes every frame. GIF frames are differences against earlier ones, so they are decoded in
    /// order into one running canvas and each result is copied out — decoding them independently
    /// would leave the partial frames that make an animation flicker and smear.
    /// </summary>
    private static AnimatedImage? DecodeAnimation(byte[] data)
    {
        List<AnimationFrame>? frames = null;
        GCHandle handle = default;

        try
        {
            using SKData skiaData = SKData.CreateCopy(data);
            using SKCodec? codec = SKCodec.Create(skiaData);
            if (codec is null)
            {
                return null;
            }

            int frameCount = codec.FrameCount;

            SKImageInfo info = new(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            int usableFrames = Math.Min(frameCount, MaxFrames);

            if (info.Width <= 0 || info.Height <= 0 || (long)info.BytesSize * usableFrames > FrameBudgetInBytes)
            {
                return null;
            }

            SKCodecFrameInfo[] frameInfo = codec.FrameInfo;
            byte[] canvas = new byte[info.BytesSize];
            handle = GCHandle.Alloc(canvas, GCHandleType.Pinned);
            IntPtr canvasAddress = handle.AddrOfPinnedObject();

            var size = new PixelSize(info.Width, info.Height);
            frames = new List<AnimationFrame>(usableFrames);

            for (int index = 0; index < usableFrames; index++)
            {
                var options = new SKCodecOptions(index);

                // Tell Skia the canvas already holds the frame this one builds on.
                if (index > 0 && index - 1 < frameInfo.Length && frameInfo[index].RequiredFrame == index - 1)
                {
                    options = new SKCodecOptions(index, index - 1);
                }

                if (codec.GetPixels(info, canvasAddress, options) is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                {
                    break;
                }

                var frame = new WriteableBitmap(size, StandardDpi, PixelFormat.Bgra8888, AlphaFormat.Premul);
                CopyInto(frame, canvas, info.RowBytes, info.Height);

                frames.Add(new AnimationFrame(frame, DurationOf(frameInfo, index)));
            }

            if (frames.Count <= 1)
            {
                DisposeAll(frames);
                return null;
            }

            return new AnimatedImage([.. frames]);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            DisposeAll(frames);
            return null;
        }
        finally
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }
    }

    private static TimeSpan DurationOf(SKCodecFrameInfo[] frameInfo, int index)
    {
        int milliseconds = index < frameInfo.Length ? frameInfo[index].Duration : 0;

        return milliseconds > 0 ? TimeSpan.FromMilliseconds(milliseconds) : AnimatedImage.DefaultFrameDuration;
    }

    /// <summary>Copies the running canvas into a frame, row by row when the strides disagree.</summary>
    private static void CopyInto(WriteableBitmap frame, byte[] canvas, int sourceStride, int height)
    {
        using ILockedFramebuffer buffer = frame.Lock();

        if (buffer.RowBytes == sourceStride)
        {
            Marshal.Copy(canvas, 0, buffer.Address, Math.Min(canvas.Length, buffer.RowBytes * height));
            return;
        }

        int copyable = Math.Min(sourceStride, buffer.RowBytes);
        for (int row = 0; row < height; row++)
        {
            Marshal.Copy(canvas, row * sourceStride, buffer.Address + (row * buffer.RowBytes), copyable);
        }
    }

    private static void DisposeAll(List<AnimationFrame>? frames)
    {
        if (frames is null)
        {
            return;
        }

        foreach (AnimationFrame frame in frames)
        {
            frame.Image.Dispose();
        }
    }
}
