using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;
using Lemmy.Views;

namespace Lemmy.Tests.Ui;

/// <summary>
/// Coming back from a post has to land where you left the feed. Losing your place is the difference
/// between a reader you can use for half an hour and one you give up on.
/// </summary>
[TestFixture]
internal sealed class ScrollRestorationTests
{
    private static void Settle() => Dispatcher.UIThread.RunJobs();

    /// <summary>The index of the first row still visible, which is what "where I was" means.</summary>
    private static int FirstVisibleRow(Visual root)
    {
        ListBox list = root.GetVisualDescendants().OfType<FeedView>().Single()
            .GetVisualDescendants().OfType<ListBox>().First();
        ScrollViewer view = FeedScroller(root);

        int best = -1;
        foreach (Control container in list.GetRealizedContainers())
        {
            double top = container.TranslatePoint(default, view)?.Y ?? 0;
            if (top + container.Bounds.Height <= 0)
            {
                continue;
            }

            int index = list.IndexFromContainer(container);
            if (index >= 0 && (best < 0 || index < best))
            {
                best = index;
            }
        }

        return best;
    }

    private static ScrollViewer FeedScroller(Visual root) =>
        root.GetVisualDescendants()
            .OfType<FeedView>()
            .Single()
            .GetVisualDescendants()
            .OfType<ScrollViewer>()
            .First();

    private static async Task<(Window Window, MainViewModel Shell)> ShowFeedAsync()
    {
        var services = new TestServices();
        services.FeedReturns(Sample.PostPage(200, null));

        var shell = new MainViewModel(services.Services, AppSettings.Default);
        await shell.InitialiseAsync();

        var window = new Window { Width = 400, Height = 700, Content = new MainView(), DataContext = shell };
        window.Show();
        Settle();

        return (window, shell);
    }

    [AvaloniaTest]
    public async Task ReturningFromAPostLandsWhereTheFeedWasLeft()
    {
        (Window window, MainViewModel shell) = await ShowFeedAsync();

        ScrollViewer scroller = FeedScroller(window);
        scroller.Offset = new Vector(0, 9000);
        Settle();
        int leftRow = FirstVisibleRow(window);
        Assert.That(leftRow, Is.GreaterThan(0), "the feed has to be scrollable for this test to mean anything");

        // Open a post the way a tap does, then come back the way the back gesture does.
        FeedViewModel feed = (FeedViewModel)shell.CurrentPage!;
        feed.Posts[3].OpenCommand.Execute(null);
        Settle();
        shell.TryGoBack();
        Settle();

        Assert.That(FirstVisibleRow(window), Is.EqualTo(leftRow));

        window.Close();
        shell.Dispose();
    }

    /// <summary>A page the reader has never scrolled must not inherit anyone else's position.</summary>
    [AvaloniaTest]
    public async Task AFreshFeedStartsAtTheTop()
    {
        (Window window, MainViewModel shell) = await ShowFeedAsync();

        Assert.That(FirstVisibleRow(window), Is.Zero);

        window.Close();
        shell.Dispose();
    }

    /// <summary>
    /// Sections keep their own root page, so switching to Communities and back should also come
    /// back to where the feed was — the same promise as returning from a post.
    /// </summary>
    [AvaloniaTest]
    public async Task SwitchingSectionsAndBackLandsWhereTheFeedWasLeft()
    {
        (Window window, MainViewModel shell) = await ShowFeedAsync();
        FeedScroller(window).Offset = new Vector(0, 700);
        Settle();
        int leftRow = FirstVisibleRow(window);
        Assert.That(leftRow, Is.GreaterThan(0));

        await shell.ShowSectionAsync(AppSection.Communities);
        Settle();
        await shell.ShowSectionAsync(AppSection.Feed);
        Settle();

        Assert.That(FirstVisibleRow(window), Is.EqualTo(leftRow));

        window.Close();
        shell.Dispose();
    }

    /// <summary>Scrolling somewhere else after returning has to be remembered in turn.</summary>
    [AvaloniaTest]
    public async Task ThePositionKeepsUpWithWhereTheReaderMovesTo()
    {
        (Window window, MainViewModel shell) = await ShowFeedAsync();
        FeedScroller(window).Offset = new Vector(0, 900);
        Settle();

        var feed = (FeedViewModel)shell.CurrentPage!;
        feed.Posts[3].OpenCommand.Execute(null);
        Settle();
        shell.TryGoBack();
        Settle();

        FeedScroller(window).Offset = new Vector(0, 16000);
        Settle();
        int movedRow = FirstVisibleRow(window);
        Assert.That(movedRow, Is.GreaterThan(0));

        feed.Posts[3].OpenCommand.Execute(null);
        Settle();
        shell.TryGoBack();
        Settle();

        Assert.That(FirstVisibleRow(window), Is.EqualTo(movedRow));

        window.Close();
        shell.Dispose();
    }
}
