using System.Collections.Immutable;
using Avalonia;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lemmy.Services;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// Mapping elapsed time onto a frame. Time drives the frame rather than a tick advancing it, so a
/// device that cannot keep up drops frames instead of playing the animation in slow motion.
/// </summary>
[TestFixture]
internal sealed class AnimatedImageTests
{
    /// <summary>Frames need a real bitmap, and a bitmap needs the platform, hence the Avalonia tests.</summary>
    private static AnimatedImage Build(params int[] durationsInMilliseconds)
    {
        var frames = ImmutableArray.CreateBuilder<AnimationFrame>(durationsInMilliseconds.Length);
        foreach (int duration in durationsInMilliseconds)
        {
            frames.Add(new AnimationFrame(
                new WriteableBitmap(new PixelSize(2, 2), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul),
                TimeSpan.FromMilliseconds(duration)));
        }

        return new AnimatedImage(frames.ToImmutable());
    }

    [AvaloniaTest]
    public void ASingleFrameIsNotAnAnimation()
    {
        using AnimatedImage picture = Build(0);

        Assert.Multiple(() =>
        {
            Assert.That(picture.IsAnimated, Is.False);
            Assert.That(picture.FrameIndexAt(TimeSpan.FromSeconds(5)), Is.Zero);
        });
    }

    [AvaloniaTest]
    public void TotalDurationIsTheSumOfTheFrames()
    {
        using AnimatedImage picture = Build(40, 80, 40, 120);

        Assert.That(picture.TotalDuration, Is.EqualTo(TimeSpan.FromMilliseconds(280)));
    }

    [AvaloniaTest]
    public void EachFrameHoldsForItsOwnDuration()
    {
        using AnimatedImage picture = Build(40, 80, 40, 120);

        Assert.Multiple(() =>
        {
            Assert.That(picture.FrameIndexAt(TimeSpan.Zero), Is.EqualTo(0));
            Assert.That(picture.FrameIndexAt(TimeSpan.FromMilliseconds(39)), Is.EqualTo(0));
            Assert.That(picture.FrameIndexAt(TimeSpan.FromMilliseconds(40)), Is.EqualTo(1));
            Assert.That(picture.FrameIndexAt(TimeSpan.FromMilliseconds(119)), Is.EqualTo(1));
            Assert.That(picture.FrameIndexAt(TimeSpan.FromMilliseconds(120)), Is.EqualTo(2));
            Assert.That(picture.FrameIndexAt(TimeSpan.FromMilliseconds(160)), Is.EqualTo(3));
        });
    }

    [AvaloniaTest]
    public void TheAnimationLoops()
    {
        using AnimatedImage picture = Build(40, 80, 40, 120);

        Assert.Multiple(() =>
        {
            Assert.That(picture.FrameIndexAt(TimeSpan.FromMilliseconds(280)), Is.EqualTo(0), "one full cycle");
            Assert.That(picture.FrameIndexAt(TimeSpan.FromMilliseconds(320)), Is.EqualTo(1));
            Assert.That(picture.FrameIndexAt(TimeSpan.FromSeconds(28)), Is.EqualTo(0), "a hundred cycles");
        });
    }

    /// <summary>A device that stalls should resume where the clock says, not where it left off.</summary>
    [AvaloniaTest]
    public void ALongStallLandsOnTheFrameTheClockCallsFor()
    {
        using AnimatedImage picture = Build(100, 100);

        Assert.That(picture.FrameIndexAt(TimeSpan.FromMilliseconds(1_100)), Is.EqualTo(1));
    }

    [AvaloniaTest]
    public void ANegativeElapsedIsTheFirstFrame()
    {
        using AnimatedImage picture = Build(40, 80);

        Assert.That(picture.FrameIndexAt(TimeSpan.FromMilliseconds(-500)), Is.Zero);
    }

    [AvaloniaTest]
    public void APictureNeedsAtLeastOneFrame() =>
        Assert.That(() => new AnimatedImage([]), Throws.TypeOf<ArgumentException>());
}
