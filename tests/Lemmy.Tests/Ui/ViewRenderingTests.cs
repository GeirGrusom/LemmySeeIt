using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lemmy;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;
using Lemmy.Views;

namespace Lemmy.Tests.Ui;

/// <summary>A gallery of exactly one picture, for tests that only care about the viewer itself.</summary>
internal sealed class SingleImageGallery(PostSummary summary) : IImageGallery
{
    public System.Collections.Immutable.ImmutableArray<PostSummary> Images { get; } = [summary];

    public bool CanLoadMore => false;

    public Task LoadMoreAsync() => Task.CompletedTask;
}

/// <summary>
/// Builds each screen for real and lays it out. Compiled bindings catch typos at build time, but a
/// binding to a property that exists on the wrong type, a missing resource key, or a template that
/// throws only show up when the control is actually realised — which is what these do.
/// </summary>
[TestFixture]
internal sealed class ViewRenderingTests
{
    private const int Width = 420;
    private const int Height = 900;

    private static Window Show(Control content, object dataContext)
    {
        var window = new Window
        {
            Width = Width,
            Height = Height,
            Content = content,
            DataContext = dataContext,
        };

        window.Show();
        Settle();
        return window;
    }

    /// <summary>Runs the pending dispatcher work — layout, bindings, render — to quiescence.</summary>
    private static void Settle() => Dispatcher.UIThread.RunJobs();

    /// <summary>Every visual in the tree, so a test can look for the text it expects to see.</summary>
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.GetVisualDescendants().OfType<Control>())
        {
            yield return child;
        }
    }

    private static IEnumerable<string> VisibleText(Control root) =>
        Descendants(root)
            .Where(control => control.IsEffectivelyVisible)
            .Select(control => control switch
            {
                // Rendered Markdown sets Inlines rather than Text, so both have to be read.
                TextBlock block => string.IsNullOrEmpty(block.Text) ? block.Inlines?.Text : block.Text,
                TextBox box => box.Text,
                _ => null,
            })
            .Where(text => !string.IsNullOrEmpty(text))!;

    private static async Task<FeedViewModel> LoadedFeedAsync(TestServices services)
    {
        services.FeedReturns(Sample.PostPage(3, null));
        var feed = new FeedViewModel(services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await feed.LoadAsync();
        return feed;
    }

    [AvaloniaTest]
    public async Task TheFeedRendersOnePostPerRow()
    {
        var services = new TestServices();
        using FeedViewModel feed = await LoadedFeedAsync(services);

        Window window = Show(new FeedView(), feed);

        Assert.Multiple(() =>
        {
            Assert.That(Descendants(window).OfType<PostCardView>().Count(), Is.EqualTo(3));
            Assert.That(VisibleText(window), Does.Contain("Post 0").And.Contain("!technology@lemmy.world"));
        });
    }

    [AvaloniaTest]
    public async Task TheFeedShowsTheScoreAndCommentCountFromTheTally()
    {
        var services = new TestServices();
        using FeedViewModel feed = await LoadedFeedAsync(services);

        Window window = Show(new FeedView(), feed);

        Assert.That(VisibleText(window), Does.Contain("12").And.Contain("5"));
    }

    [AvaloniaTest]
    public async Task TheFeedShowsTheServersMessageWhenALoadFails()
    {
        var services = new TestServices();
        services.Api.GetFeedAsync(Arg.Any<Lemmy.Api.FeedQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<PostPage>>(_ => throw new Lemmy.Api.LemmyApiException("lemmy.world answered 502."));
        using var feed = new FeedViewModel(services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await feed.LoadAsync();

        Window window = Show(new FeedView(), feed);

        Assert.That(VisibleText(window), Does.Contain("lemmy.world answered 502.").And.Contain("Try again"));
    }

    [AvaloniaTest]
    public async Task APostRendersItsCommentThreadNested()
    {
        var services = new TestServices();
        services.Api.GetCommentsAsync(Arg.Any<Lemmy.Api.CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommentThread(
            [
                Sample.CommentNode(100, "0.100", 1, Sample.CommentNode(200, "0.100.200")),
            ])));
        using var page = new PostDetailViewModel(
            services.Services, new RecordingNavigator(), services.Api, Sample.PostSummary(), AppSettings.Default);
        await page.LoadAsync();

        Window window = Show(new PostDetailView(), page);

        Assert.Multiple(() =>
        {
            Assert.That(Descendants(window).OfType<CommentView>().Count(), Is.EqualTo(2), "the reply nests inside its parent");
            Assert.That(VisibleText(window), Does.Contain("A post").And.Contain("A comment"));
        });
    }

    [AvaloniaTest]
    public async Task CollapsingACommentHidesItsRepliesAndFlipsTheGlyph()
    {
        var services = new TestServices();
        services.Api.GetCommentsAsync(Arg.Any<Lemmy.Api.CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommentThread(
            [
                Sample.CommentNode(100, "0.100", 1, Sample.CommentNode(200, "0.100.200")),
            ])));
        using var page = new PostDetailViewModel(
            services.Services, new RecordingNavigator(), services.Api, Sample.PostSummary(), AppSettings.Default);
        await page.LoadAsync();
        Window window = Show(new PostDetailView(), page);

        page.Comments[0].ToggleCollapsedCommand.Execute(null);
        Settle();

        Assert.That(
            Descendants(window).OfType<CommentView>().Count(view => view.IsEffectivelyVisible),
            Is.EqualTo(1));
    }

    [AvaloniaTest]
    public void TheSearchScreenStartsWithItsPrompt()
    {
        var services = new TestServices();
        using var search = new SearchViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);

        Window window = Show(new SearchView(), search);

        Assert.That(VisibleText(window), Does.Contain("Search this instance for posts and communities."));
    }

    [AvaloniaTest]
    public async Task TheShellShowsTheCurrentPageAndItsTitle()
    {
        var services = new TestServices();
        services.FeedReturns(Sample.PostPage(2, null));
        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        await shell.InitialiseAsync();

        Window window = Show(new MainView(), shell);

        Assert.Multiple(() =>
        {
            Assert.That(Descendants(window).OfType<FeedView>().Count(), Is.EqualTo(1), "the view locator resolved the feed");
            Assert.That(VisibleText(window), Does.Contain("lemmy.world").And.Contain("Communities"));
        });
    }

    [AvaloniaTest]
    public async Task TheSettingsSheetAppearsWhenAskedFor()
    {
        var services = new TestServices();
        services.FeedReturns(Sample.PostPage(1, null));
        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        await shell.InitialiseAsync();
        Window window = Show(new MainView(), shell);

        shell.ToggleInstancePickerCommand.Execute(null);
        Settle();

        Assert.That(VisibleText(window), Does.Contain("Settings").And.Contain("Show content flagged NSFW"));
    }

    /// <summary>
    /// The desktop head hosts the very same view, so if this window lays out then Windows and Linux
    /// are showing what Android and iOS show.
    /// </summary>
    [AvaloniaTest]
    public async Task TheDesktopWindowHostsTheSharedShell()
    {
        var services = new TestServices();
        services.FeedReturns(Sample.PostPage(1, null));
        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        await shell.InitialiseAsync();

        var window = new MainWindow { DataContext = shell, Width = 900, Height = 700 };
        window.Show();
        Settle();

        Assert.Multiple(() =>
        {
            Assert.That(Descendants(window).OfType<MainView>().Count(), Is.EqualTo(1));
            Assert.That(window.Title, Is.EqualTo("LemmySeeIt"));
        });

        window.Close();
    }

    /// <summary>
    /// Android 15 forces every app targeting SDK 35+ to draw edge to edge, and the shell relies on
    /// Avalonia insetting it for the status bar and the gesture pill. That is the framework default
    /// rather than something this app turns on, which is exactly why it is worth pinning: nothing
    /// on screen would announce the regression if a later change switched it off.
    /// </summary>
    [AvaloniaTest]
    public void TheShellOptsIntoSafeAreaPadding() =>
        Assert.That(TopLevel.GetAutoSafeAreaPadding(new MainView()), Is.True);

    /// <summary>The system back gesture has to reach the shell, and let go of it when the view does.</summary>
    [AvaloniaTest]
    public async Task TheShellAnswersTheSystemBackRequestWhileItIsOnScreen()
    {
        var services = new TestServices();
        services.FeedReturns(Sample.PostPage(1, null));
        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        await shell.InitialiseAsync();
        PageViewModel? feed = shell.CurrentPage;
        Window window = Show(new MainView(), shell);
        shell.Push(new SearchViewModel(services.Services, shell, services.Api, AppSettings.Default));

        window.RaiseEvent(new RoutedEventArgs(TopLevel.BackRequestedEvent) { RoutedEvent = TopLevel.BackRequestedEvent });
        Settle();

        Assert.That(shell.CurrentPage, Is.SameAs(feed));
    }

    /// <summary>
    /// Pull-to-refresh is the gesture people actually use on a phone; the toolbar button is the
    /// desktop affordance. <c>RequestRefresh</c> raises exactly what a real pull raises, so this
    /// exercises the wiring rather than the touch handling.
    /// </summary>
    [AvaloniaTest]
    public async Task PullingTheFeedDownRefetchesIt()
    {
        var services = new TestServices();
        using FeedViewModel feed = await LoadedFeedAsync(services);
        Window window = Show(new FeedView(), feed);
        RefreshContainer refresher = Descendants(window).OfType<RefreshContainer>().Single();

        refresher.RequestRefresh();
        Settle();

        await services.Api.Received(2).GetFeedAsync(Arg.Any<Lemmy.Api.FeedQuery>(), Arg.Any<CancellationToken>());
    }

    [AvaloniaTest]
    public async Task PullingTheCommunityDirectoryDownRefetchesIt()
    {
        var services = new TestServices();
        services.Api.GetCommunitiesAsync(Arg.Any<Lemmy.Api.CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(System.Collections.Immutable.ImmutableArray.Create(Sample.CommunitySummary())));
        using var communities = new CommunitiesViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await communities.LoadAsync();
        Window window = Show(new CommunitiesView(), communities);

        Descendants(window).OfType<RefreshContainer>().Single().RequestRefresh();
        Settle();

        await services.Api.Received(2).GetCommunitiesAsync(Arg.Any<Lemmy.Api.CommunityQuery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The gesture stays off for mouse users: on desktop the toolbar button is the affordance, and
    /// a mouse drag that silently reloads the list would be a surprise.
    /// </summary>
    [AvaloniaTest]
    public async Task PullToRefreshIsNotArmedForAMouse()
    {
        var services = new TestServices();
        using FeedViewModel feed = await LoadedFeedAsync(services);
        Window window = Show(new FeedView(), feed);

        Assert.That(Descendants(window).OfType<RefreshContainer>().Single().IsMouseEnabled, Is.False);
    }

    /// <summary>The picture has to cover the shell, not sit inside it.</summary>
    [AvaloniaTest]
    public async Task TheImageViewerCoversTheWholeShell()
    {
        var services = new TestServices();
        services.FeedReturns(Sample.PostPage(3, null));
        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        await shell.InitialiseAsync();
        Window window = Show(new MainView(), shell);

        shell.ShowImage(Sample.ImagePostSummary(), new SingleImageGallery(Sample.ImagePostSummary()));
        Settle();

        ImageViewerView viewer = Descendants(window).OfType<ImageViewerView>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(viewer.IsEffectivelyVisible, Is.True);
            Assert.That(viewer.Bounds.Height, Is.EqualTo(window.Bounds.Height).Within(1.0));
            Assert.That(VisibleText(window), Does.Contain("A picture"));
        });
    }

    [AvaloniaTest]
    public async Task ClosingTheImageViewerLeavesTheFeedExactlyWhereItWas()
    {
        var services = new TestServices();
        services.FeedReturns(Sample.PostPage(3, null));
        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        await shell.InitialiseAsync();
        Window window = Show(new MainView(), shell);

        shell.ShowImage(Sample.ImagePostSummary(), new SingleImageGallery(Sample.ImagePostSummary()));
        Settle();
        shell.TryGoBack();
        Settle();

        Assert.Multiple(() =>
        {
            Assert.That(Descendants(window).OfType<ImageViewerView>(), Is.Empty);
            Assert.That(Descendants(window).OfType<FeedView>().Count(), Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Tap-to-dismiss and double-tap-to-zoom start with the same tap, so closing on the first
    /// release fires halfway through every double-tap — on the device that closed the viewer and
    /// let the second tap open a post behind it. The close has to wait out the double-tap window.
    /// </summary>
    [AvaloniaTest]
    public async Task ATapDoesNotDismissTheImageViewerStraightAway()
    {
        var services = new TestServices();
        services.FeedReturns(Sample.PostPage(3, null));
        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        await shell.InitialiseAsync();
        Window window = Show(new MainView(), shell);

        shell.ShowImage(Sample.ImagePostSummary(), new SingleImageGallery(Sample.ImagePostSummary()));
        Settle();

        var point = new Point(200, 500);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();

        Assert.That(shell.ImageViewer, Is.Not.Null, "a second tap may still be coming");
    }

    /// <summary>
    /// A link post's URL used to be a line of text with no way to follow it. It has to be something
    /// you can press.
    /// </summary>
    [AvaloniaTest]
    public async Task ALinkPostOffersItsUrlAsSomethingPressable()
    {
        var services = new TestServices();
        PostSummary summary = Sample.PostSummary() with
        {
            Post = Sample.Post(url: "https://apnews.com/article/x", contentType: "text/html"),
        };
        using var page = new PostDetailViewModel(
            services.Services, new RecordingNavigator(), services.Api, summary, AppSettings.Default);
        await page.LoadAsync();

        Window window = Show(new PostDetailView(), page);

        Button? linkButton = Descendants(window)
            .OfType<Button>()
            .FirstOrDefault(button => button.Command == page.OpenLinkCommand);

        Assert.Multiple(() =>
        {
            Assert.That(linkButton, Is.Not.Null);
            Assert.That(linkButton!.IsEffectivelyVisible, Is.True);
            Assert.That(VisibleText(window), Does.Contain("https://apnews.com/article/x"));
            Assert.That(VisibleText(window), Does.Contain("Open on the web"));
        });
    }

    /// <summary>Renders to pixels, which is the only way to prove the styles resolve to something drawable.</summary>
    [AvaloniaTest]
    public async Task TheFeedActuallyDrawsPixels()
    {
        var services = new TestServices();
        using FeedViewModel feed = await LoadedFeedAsync(services);
        Window window = Show(new FeedView(), feed);

        using Bitmap? frame = window.CaptureRenderedFrame();

        Assert.That(frame, Is.Not.Null);
        Assert.That(frame!.PixelSize.Width, Is.GreaterThan(0));
    }

    /// <summary>
    /// The licences page is built from generated data and embedded text, so a wiring mistake would
    /// show as an empty page rather than as a failure anywhere else.
    /// </summary>
    [AvaloniaTest]
    public void TheLicencesPageListsThePackagesTheBuildShips()
    {
        var services = new TestServices();
        using var page = new AttributionViewModel(services.Services, new RecordingNavigator());

        Window window = Show(new AttributionView(), page);

        Assert.Multiple(() =>
        {
            Assert.That(VisibleText(window), Does.Contain("Avalonia"));
            Assert.That(VisibleText(window), Does.Contain("Third-party packages"));
            Assert.That(page.Packages, Is.Not.Empty);
        });
    }

    [AvaloniaTest]
    public void TheLicencesPageShowsTheApplicationLicenceInFull()
    {
        var services = new TestServices();
        using var page = new AttributionViewModel(services.Services, new RecordingNavigator());

        Window window = Show(new AttributionView(), page);

        Assert.That(
            VisibleText(window).Any(text => text.Contains("WITHOUT WARRANTY OF ANY KIND", StringComparison.Ordinal)),
            Is.True);
    }

    [AvaloniaTest]
    public void TheLicencesPageOffersTheTextOfEveryLicenceInPlay()
    {
        var services = new TestServices();
        using var page = new AttributionViewModel(services.Services, new RecordingNavigator());

        Window window = Show(new AttributionView(), page);

        Assert.Multiple(() =>
        {
            Assert.That(page.LicenceTexts, Is.Not.Empty);
            Assert.That(page.LicenceTexts.Select(licence => licence.Identifier), Does.Contain("MIT"));
            Assert.That(VisibleText(window), Does.Contain("MIT"));
        });
    }

    [AvaloniaTest]
    public void TheLicencesPageDrawsPixels()
    {
        var services = new TestServices();
        using var page = new AttributionViewModel(services.Services, new RecordingNavigator());
        Window window = Show(new AttributionView(), page);

        using Bitmap? frame = window.CaptureRenderedFrame();

        Assert.That(frame, Is.Not.Null);
        Assert.That(frame!.PixelSize.Width, Is.GreaterThan(0));
    }

    [AvaloniaTest]
    public async Task SignedOutAFeedRowShowsTheScoreWithNoArrows()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(false);
        using FeedViewModel feed = await LoadedFeedAsync(services);

        Window window = Show(new FeedView(), feed);

        Control[] arrows = [.. Descendants(window)
            .OfType<Button>()
            .Where(button => button.Classes.Contains("vote") && button.IsEffectivelyVisible)];

        Assert.Multiple(() =>
        {
            Assert.That(arrows, Is.Empty);
            Assert.That(VisibleText(window), Does.Contain("12"));
        });
    }

    [AvaloniaTest]
    public async Task SignedInAFeedRowOffersBothArrows()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        using FeedViewModel feed = await LoadedFeedAsync(services);

        Window window = Show(new FeedView(), feed);

        Button[] arrows = [.. Descendants(window)
            .OfType<Button>()
            .Where(button => button.Classes.Contains("vote") && button.IsEffectivelyVisible)];

        Assert.Multiple(() =>
        {
            // Three rows, two arrows each.
            Assert.That(arrows, Has.Length.EqualTo(6));
            Assert.That(arrows.Count(button => button.Classes.Contains("up")), Is.EqualTo(3));
            Assert.That(arrows.Count(button => button.Classes.Contains("down")), Is.EqualTo(3));
        });
    }

    [AvaloniaTest]
    public async Task PressingTheUpArrowOnARowVotesThroughTheApi()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        services.Api.VoteOnPostAsync(Arg.Any<PostId>(), Arg.Any<Vote>(), Arg.Any<CancellationToken>())
            .Returns(new VoteOutcome(Vote.Up, new Score(99), new VoteCount(100), new VoteCount(1)));

        using FeedViewModel feed = await LoadedFeedAsync(services);
        Window window = Show(new FeedView(), feed);

        Button upArrow = Descendants(window)
            .OfType<Button>()
            .First(button => button.Classes.Contains("vote") && button.Classes.Contains("up"));

        upArrow.Command!.Execute(null);
        Settle();

        await services.Api.Received(1).VoteOnPostAsync(Arg.Any<PostId>(), Vote.Up, Arg.Any<CancellationToken>());
        Assert.That(feed.Posts[0].Votes.IsUpvoted, Is.True);
    }
}
