using System.Collections.Immutable;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lemmy.Domain;
using Lemmy.Domain.Markdown;
using Lemmy.Services;
using Lemmy.ViewModels;
using Lemmy.Views.Markdown;

namespace Lemmy.Tests.Ui;

/// <summary>
/// Pictures written into a body. They stay folded away until asked for, and nothing is fetched
/// before that — a thread can carry dozens.
/// </summary>
[TestFixture]
internal sealed class MarkdownImageTests
{
    private static readonly WebLink Picture = new("https://example.com/chart.png");

    private static ImmutableArray<MarkdownBlock> Blocks() =>
        MarkdownParser.Parse(new MarkdownText("Look: ![a chart](https://example.com/chart.png)"));

    private static (MarkdownView View, Window Window) Show(MarkdownMedia media)
    {
        var view = new MarkdownView { Blocks = Blocks(), Media = media };
        var window = new Window { Width = 420, Height = 700, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (view, window);
    }

    private static Expander Expander(Window window) =>
        window.GetVisualDescendants().OfType<Expander>().Single();

    [AvaloniaTest]
    public void APictureStartsFoldedAwayBehindItsAltText()
    {
        IImageLoader loader = Substitute.For<IImageLoader>();
        (_, Window window) = Show(new MarkdownMedia(loader, null));

        Expander expander = Expander(window);

        Assert.Multiple(() =>
        {
            Assert.That(expander.IsExpanded, Is.False);
            Assert.That(expander.Header?.ToString(), Does.Contain("a chart"));
            Assert.That(window.GetVisualDescendants().OfType<Image>(), Is.Empty);
        });
    }

    [AvaloniaTest]
    public void NothingIsFetchedUntilTheReaderAsks()
    {
        IImageLoader loader = Substitute.For<IImageLoader>();
        Show(new MarkdownMedia(loader, null));

        loader.DidNotReceive().LoadPictureAsync(Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [AvaloniaTest]
    public async Task ExpandingFetchesThePictureAndShowsIt()
    {
        IImageLoader loader = Substitute.For<IImageLoader>();
        loader.LoadPictureAsync(Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Still());

        (_, Window window) = Show(new MarkdownMedia(loader, null));
        Expander(window).IsExpanded = true;

        await Task.Yield();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(window.GetVisualDescendants().OfType<Image>().Count(), Is.EqualTo(1));
            Assert.That(
                loader.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IImageLoader.LoadPictureAsync)),
                Is.EqualTo(1));
        });
    }

    [AvaloniaTest]
    public async Task ExpandingTwiceDoesNotFetchTwice()
    {
        IImageLoader loader = Substitute.For<IImageLoader>();
        loader.LoadPictureAsync(Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Still());

        (_, Window window) = Show(new MarkdownMedia(loader, null));
        Expander expander = Expander(window);

        expander.IsExpanded = true;
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();

        expander.IsExpanded = false;
        expander.IsExpanded = true;
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();

        Assert.That(
            loader.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IImageLoader.LoadPictureAsync)),
            Is.EqualTo(1));
    }

    [AvaloniaTest]
    public async Task APictureThatWillNotLoadSaysSoRatherThanStayingBlank()
    {
        IImageLoader loader = Substitute.For<IImageLoader>();
        loader.LoadPictureAsync(Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((AnimatedImage?)null);

        (_, Window window) = Show(new MarkdownMedia(loader, null));
        Expander(window).IsExpanded = true;

        await Task.Yield();
        Dispatcher.UIThread.RunJobs();

        Assert.That(
            window.GetVisualDescendants().OfType<TextBlock>().Any(t => (t.Text ?? "").Contains("could not be loaded")),
            Is.True);
    }

    [AvaloniaTest]
    public async Task TappingTheOpenedPictureSendsItToBeShownFullScreen()
    {
        var navigator = new TestSupport.RecordingNavigator();
        IImageLoader loader = Substitute.For<IImageLoader>();
        loader.LoadPictureAsync(Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Still());

        var open = new CommunityToolkit.Mvvm.Input.RelayCommand<MarkdownImage>(
            image => navigator.ShowPicture(image!.Source, image.AltText));

        (_, Window window) = Show(new MarkdownMedia(loader, open));
        Expander(window).IsExpanded = true;
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();

        Image picture = window.GetVisualDescendants().OfType<Image>().Single();
        open.Execute(new MarkdownImage(Picture, "a chart"));

        Assert.Multiple(() =>
        {
            Assert.That(picture.Source, Is.Not.Null);
            Assert.That(navigator.PicturesShown, Has.Count.EqualTo(1));
            Assert.That(navigator.PicturesShown[0].Picture, Is.EqualTo(Picture));
            Assert.That(navigator.PicturesShown[0].Caption, Is.EqualTo("a chart"));
        });
    }

    /// <summary>A one-frame picture, which is what a still is.</summary>
    private static AnimatedImage Still() =>
        new([new AnimationFrame(new RenderTargetBitmap(new Avalonia.PixelSize(8, 8)), TimeSpan.Zero)]);

    /// <summary>A picture with more than one frame, which should move.</summary>
    private static AnimatedImage Moving() =>
        new(
        [
            new AnimationFrame(new RenderTargetBitmap(new Avalonia.PixelSize(8, 8)), TimeSpan.FromMilliseconds(50)),
            new AnimationFrame(new RenderTargetBitmap(new Avalonia.PixelSize(8, 8)), TimeSpan.FromMilliseconds(50)),
        ]);

    [AvaloniaTest]
    public async Task AGifIsDecodedWithEveryFrameRatherThanJustTheFirst()
    {
        IImageLoader loader = Substitute.For<IImageLoader>();
        AnimatedImage moving = Moving();
        loader.LoadPictureAsync(Arg.Any<WebLink>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(moving);

        (_, Window window) = Show(new MarkdownMedia(loader, null));
        Expander(window).IsExpanded = true;
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();

        Image shown = window.GetVisualDescendants().OfType<Image>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(moving.IsAnimated, Is.True);
            Assert.That(shown.Source, Is.SameAs(moving.FirstFrame), "it starts on the first frame");
        });
    }

    [AvaloniaTest]
    public void APictureWithNoAltTextIsJustCalledImage()
    {
        IImageLoader loader = Substitute.For<IImageLoader>();
        var view = new MarkdownView
        {
            Blocks = MarkdownParser.Parse(new MarkdownText("![](https://example.com/chart.png)")),
            Media = new MarkdownMedia(loader, null),
        };
        var window = new Window { Width = 420, Height = 400, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.That(Expander(window).Header?.ToString(), Is.EqualTo("Image"));
    }

    [AvaloniaTest]
    public void APictureWithAltTextSaysWhatItIs()
    {
        IImageLoader loader = Substitute.For<IImageLoader>();
        (_, Window window) = Show(new MarkdownMedia(loader, null));

        Assert.That(Expander(window).Header?.ToString(), Is.EqualTo("Image — a chart"));
    }
}
