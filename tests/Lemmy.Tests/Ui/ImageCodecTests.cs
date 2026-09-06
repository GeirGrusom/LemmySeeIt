using System.Net;
using System.Runtime.InteropServices;
using Avalonia.Platform;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.ViewModels;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;

namespace Lemmy.Tests.Ui;

/// <summary>
/// Which image formats the app can actually show. Avalonia decodes through Skia, so the answer is
/// whatever the bundled native build supports rather than anything this repository decides — which
/// is exactly why it is asserted against real files instead of assumed.
/// </summary>
[TestFixture]
internal sealed class ImageCodecTests
{
    private static string Asset(string name) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", name);

    private static async Task<AnimatedImage?> LoadThroughTheAppAsync(string assetName) =>
        (await LoadResultAsync(assetName)).Picture;

    private static async Task<PictureLoad> LoadResultAsync(string assetName)
    {
        byte[] bytes = await File.ReadAllBytesAsync(Asset(assetName));

        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes),
        });

        using var loader = new ImageLoader(handler.CreateClient());
        return await loader.LoadPictureAsync(WebLink.Parse("https://example.com/image"), 400);
    }

    [AvaloniaTest]
    public async Task JpegDecodes()
    {
        using AnimatedImage? picture = await LoadThroughTheAppAsync("sample.jpg");

        Assert.That(picture, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(picture!.FirstFrame.PixelSize.Width, Is.EqualTo(400));
            Assert.That(picture.IsAnimated, Is.False);
        });
    }

    /// <summary>
    /// About a fifth of the picture posts on the instances sampled are WebP, so this one is not
    /// academic.
    /// </summary>
    [AvaloniaTest]
    public async Task WebpDecodes()
    {
        using AnimatedImage? picture = await LoadThroughTheAppAsync("sample.webp");

        Assert.That(picture, Is.Not.Null);
        Assert.That(picture!.FirstFrame.PixelSize.Width, Is.EqualTo(400));
    }

    /// <summary>
    /// AVIF is not in the Skia build Avalonia ships, and the decoder does not fail politely — it
    /// throws from inside the platform. An undecodable picture has to come back as "no picture", or
    /// the caller is left holding an exception it cannot do anything with and a spinner that never
    /// stops.
    /// </summary>
    [AvaloniaTest]
    public async Task AnUndecodableFormatComesBackAsNothingRatherThanThrowing()
    {
        // Awaited rather than wrapped in a Throws constraint: that constraint blocks waiting on the
        // task, and on Avalonia's single dispatcher thread blocking is a deadlock.
        AnimatedImage? picture = null;
        Exception? thrown = null;

        try
        {
            picture = await LoadThroughTheAppAsync("sample.avif");
        }
        catch (Exception exception)
        {
            thrown = exception;
        }

        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.Null, "an undecodable picture is a result, not an error");
            Assert.That(picture, Is.Null);
        });
    }

    /// <summary>
    /// The whole path with real bytes: the loader has to say which format defeated it, or the
    /// reader is told the same thing for an AVIF as for a dead link and cannot tell the difference
    /// between "never going to work" and "try again".
    /// </summary>
    [AvaloniaTest]
    public async Task AnUndecodableFormatIsNamedRatherThanLumpedInWithEveryOtherFailure()
    {
        PictureLoad load = await LoadResultAsync("sample.avif");

        Assert.Multiple(() =>
        {
            Assert.That(load.Failure, Is.EqualTo(ImageFailure.UnsupportedFormat));
            Assert.That(load.FormatName, Is.EqualTo("AVIF"));
            Assert.That(load.Message, Is.EqualTo("That picture is an AVIF, which this app cannot show."));
        });
    }

    [AvaloniaTest]
    public async Task APictureThatDecodesReportsNoFailure()
    {
        PictureLoad load = await LoadResultAsync("sample.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(load.Succeeded, Is.True);
            Assert.That(load.Failure, Is.EqualTo(ImageFailure.None));
            Assert.That(load.Message, Is.Empty);
        });

        load.Picture?.Dispose();
    }

    /// <summary>A link that answers with nothing usable is a different problem from a bad format.</summary>
    [AvaloniaTest]
    public async Task AnUnreachablePictureIsToldApartFromAnUndecodableOne()
    {
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var loader = new ImageLoader(handler.CreateClient());

        PictureLoad load = await loader.LoadPictureAsync(WebLink.Parse("https://example.com/gone.png"), 400);

        Assert.Multiple(() =>
        {
            Assert.That(load.Failure, Is.EqualTo(ImageFailure.Unreachable));
            Assert.That(load.Message, Does.Contain("could not be downloaded"));
        });
    }

    /// <summary>
    /// The whole path, with real bytes: an AVIF original the platform cannot decode, a JPEG preview
    /// it can. The reader gets the picture at preview quality and is told that is what they are
    /// looking at, rather than getting an error over a post that is perfectly viewable.
    /// </summary>
    [AvaloniaTest]
    public async Task AnAvifPostFallsBackToItsPreviewAndSaysSo()
    {
        byte[] avif = await File.ReadAllBytesAsync(Asset("sample.avif"));
        byte[] jpeg = await File.ReadAllBytesAsync(Asset("sample.jpg"));

        using var handler = new StubHttpMessageHandler(request =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(
                    request.RequestUri!.AbsolutePath.EndsWith(".avif", StringComparison.Ordinal) ? avif : jpeg),
            });

        using var loader = new ImageLoader(handler.CreateClient());

        PostSummary post = Sample.PostSummary() with
        {
            Post = Sample.Post(url: "https://example.com/art.avif", contentType: "image/avif") with
            {
                Thumbnail = WebLink.Parse("https://example.com/preview.jpeg"),
            },
        };

        using var viewer = new ImageViewerViewModel(new SingleImageGallery(post), post, loader, () => { });
        await viewer.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewer.Image, Is.Not.Null, "the preview is perfectly decodable");
            Assert.That(viewer.IsShowingReducedQuality, Is.True);
            Assert.That(viewer.HasFailed, Is.False);
        });
    }

    /// <summary>
    /// The GIF is built by the test-asset generator with four frames of known colour and known
    /// delays, so this asserts the decoder rather than trusting it.
    /// </summary>
    [AvaloniaTest]
    public async Task AnAnimatedGifDecodesEveryFrameWithItsOwnTiming()
    {
        using AnimatedImage? picture = await LoadThroughTheAppAsync("animated.gif");

        Assert.That(picture, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(picture!.IsAnimated, Is.True);
            Assert.That(picture.Frames, Has.Length.EqualTo(4));
            Assert.That(
                picture.Frames.Select(frame => frame.Duration.TotalMilliseconds),
                Is.EqualTo(new[] { 40d, 80d, 40d, 120d }));
            Assert.That(picture.TotalDuration, Is.EqualTo(TimeSpan.FromMilliseconds(280)));
        });
    }

    /// <summary>
    /// Frames are decoded one after another into a single running canvas, so each one has to be
    /// copied out. If it were not, every frame would alias the same pixels and the animation would
    /// be four copies of its last frame — which looks like nothing happening at all.
    /// </summary>
    [AvaloniaTest]
    public async Task EachDecodedFrameHoldsItsOwnPixels()
    {
        using AnimatedImage? picture = await LoadThroughTheAppAsync("animated.gif");

        List<uint> colours = [.. picture!.Frames.Select(frame => TopLeftPixel((WriteableBitmap)frame.Image))];

        Assert.That(colours, Is.Unique, "the four frames are four different solid colours");
    }

    /// <summary>Reads one pixel back out of a decoded frame.</summary>
    private static uint TopLeftPixel(WriteableBitmap frame)
    {
        using ILockedFramebuffer buffer = frame.Lock();
        byte[] pixel = new byte[4];
        Marshal.Copy(buffer.Address, pixel, 0, 4);

        return BitConverter.ToUInt32(pixel);
    }

    /// <summary>A still picture is an animation of one frame, so callers never special-case it.</summary>
    [AvaloniaTest]
    public async Task AStillPictureIsASingleFrame()
    {
        using AnimatedImage? picture = await LoadThroughTheAppAsync("sample.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(picture!.Frames, Has.Length.EqualTo(1));
            Assert.That(picture.IsAnimated, Is.False);
        });
    }

    /// <summary>Feed rows stay still: a page of animating thumbnails is a worse page.</summary>
    [AvaloniaTest]
    public async Task AThumbnailOfAnAnimatedGifIsStill()
    {
        byte[] bytes = await File.ReadAllBytesAsync(Asset("animated.gif"));
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes),
        });
        using var loader = new ImageLoader(handler.CreateClient());

        using Bitmap? thumbnail = await loader.LoadAsync(WebLink.Parse("https://example.com/a.gif"), 64);

        Assert.That(thumbnail, Is.Not.Null);
        Assert.That(thumbnail, Is.Not.InstanceOf<WriteableBitmap>(), "the still path does not build frames");
    }

    /// <summary>The viewer swaps frames as the clock advances, and says that it is animating.</summary>
    [AvaloniaTest]
    public async Task TheViewerPlaysAnAnimatedPicture()
    {
        byte[] gif = await File.ReadAllBytesAsync(Asset("animated.gif"));
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(gif),
        });
        using var loader = new ImageLoader(handler.CreateClient());

        PostSummary post = Sample.PostSummary() with
        {
            Post = Sample.Post(url: "https://example.com/a.gif", contentType: "image/gif"),
        };

        using var viewer = new ImageViewerViewModel(new SingleImageGallery(post), post, loader, () => { });
        await viewer.LoadAsync();

        Assert.That(viewer.IsAnimated, Is.True);

        viewer.ShowFrameAt(TimeSpan.Zero);
        Bitmap? first = viewer.Image;

        viewer.ShowFrameAt(TimeSpan.FromMilliseconds(200));
        Bitmap? later = viewer.Image;

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Null);
            Assert.That(later, Is.Not.SameAs(first), "the clock moved on, so the frame should have");
        });
    }

    /// <summary>A still picture ignores the clock rather than flickering against it.</summary>
    [AvaloniaTest]
    public async Task AStillPictureDoesNotRespondToTheClock()
    {
        byte[] jpeg = await File.ReadAllBytesAsync(Asset("sample.jpg"));
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(jpeg),
        });
        using var loader = new ImageLoader(handler.CreateClient());

        PostSummary post = Sample.ImagePostSummary();
        using var viewer = new ImageViewerViewModel(new SingleImageGallery(post), post, loader, () => { });
        await viewer.LoadAsync();

        Bitmap? shown = viewer.Image;
        viewer.ShowFrameAt(TimeSpan.FromSeconds(3));

        Assert.Multiple(() =>
        {
            Assert.That(viewer.IsAnimated, Is.False);
            Assert.That(viewer.Image, Is.SameAs(shown));
        });
    }

    [AvaloniaTest]
    public async Task RubbishBytesAreAlsoJustNothing()
    {
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0x00, 0x01, 0x02, 0x03, 0x04]),
        });
        using var loader = new ImageLoader(handler.CreateClient());

        PictureLoad load = await loader.LoadPictureAsync(WebLink.Parse("https://example.com/image"), 400);

        Assert.That(load.Picture, Is.Null);
    }
}
