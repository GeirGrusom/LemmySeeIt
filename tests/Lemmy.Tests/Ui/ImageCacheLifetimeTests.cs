using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Lemmy.Domain;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;

namespace Lemmy.Tests.Ui;

/// <summary>
/// Who owns a cached bitmap, and for how long.
/// </summary>
/// <remarks>
/// A thumbnail is handed out and then held for as long as the row showing it exists, and rows
/// outlive their place in the cache — sections stay alive when the reader switches away. So the
/// cache can reach its cap and evict a bitmap the feed is still bound to. Disposing on eviction
/// made that a crash on the next layout pass, which is exactly how it was reported: browse a
/// community or a search, come back to Feed, and it dies reading the size of a dead bitmap.
/// </remarks>
[TestFixture]
internal sealed class ImageCacheLifetimeTests
{
    /// <summary>Comfortably past the cache's cap, so eviction certainly happens.</summary>
    private const int EnoughToEvict = 260;

    private static async Task<(ImageLoader Loader, StubHttpMessageHandler Handler)> LoaderAsync()
    {
        byte[] jpeg = await File.ReadAllBytesAsync(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "sample.jpg"));

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(jpeg),
        });

        return (new ImageLoader(handler.CreateClient()), handler);
    }

    [AvaloniaTest]
    public async Task ABitmapStaysUsableAfterTheCacheHasEvictedIt()
    {
        (ImageLoader loader, StubHttpMessageHandler handler) = await LoaderAsync();

        using (handler)
        using (loader)
        {
            Bitmap? first = await loader.LoadAsync(WebLink.Parse("https://example.com/0.jpg"), 64);
            Assert.That(first, Is.Not.Null);

            for (int index = 1; index <= EnoughToEvict; index++)
            {
                await loader.LoadAsync(WebLink.Parse($"https://example.com/{index}.jpg"), 64);
            }

            // The reported crash was Bitmap.get_Size on a disposed bitmap, from Image.MeasureOverride.
            Assert.That(() => first!.Size, Throws.Nothing);
        }
    }

    /// <summary>The whole path: a bound Image must survive a layout pass after eviction.</summary>
    [AvaloniaTest]
    public async Task AnImageStillOnScreenSurvivesEviction()
    {
        (ImageLoader loader, StubHttpMessageHandler handler) = await LoaderAsync();

        using (handler)
        using (loader)
        {
            Bitmap? shown = await loader.LoadAsync(WebLink.Parse("https://example.com/shown.jpg"), 64);

            var image = new Image { Source = shown };
            var window = new Window { Width = 200, Height = 200, Content = new Border { Child = image } };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            for (int index = 0; index <= EnoughToEvict; index++)
            {
                await loader.LoadAsync(WebLink.Parse($"https://example.com/other-{index}.jpg"), 64);
            }

            // Force the measure pass that used to throw.
            image.InvalidateMeasure();

            Assert.That(
                () =>
                {
                    window.Measure(new Size(200, 200));
                    Dispatcher.UIThread.RunJobs();
                },
                Throws.Nothing);

            window.Close();
        }
    }

    /// <summary>Disposing the loader must not kill bitmaps a view is still showing either.</summary>
    [AvaloniaTest]
    public async Task DisposingTheLoaderDoesNotKillBitmapsStillInUse()
    {
        (ImageLoader loader, StubHttpMessageHandler handler) = await LoaderAsync();
        Bitmap? held;

        using (handler)
        {
            held = await loader.LoadAsync(WebLink.Parse("https://example.com/held.jpg"), 64);
            loader.Dispose();
        }

        Assert.That(() => held!.Size, Throws.Nothing);
    }

    /// <summary>The cache still stops growing; it just stops destroying what it lets go of.</summary>
    [AvaloniaTest]
    public async Task TheCacheStillHasABound()
    {
        (ImageLoader loader, StubHttpMessageHandler handler) = await LoaderAsync();

        using (handler)
        using (loader)
        {
            for (int index = 0; index <= EnoughToEvict; index++)
            {
                await loader.LoadAsync(WebLink.Parse($"https://example.com/{index}.jpg"), 64);
            }

            // Re-requesting the very first one has to go back to the network, which is what proves
            // it left the cache rather than simply never being evicted.
            handler.Reset();
            await loader.LoadAsync(WebLink.Parse("https://example.com/0.jpg"), 64);

            Assert.That(handler.RequestedUris, Is.Not.Empty);
        }
    }
}
