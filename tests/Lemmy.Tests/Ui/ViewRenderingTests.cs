using System.Collections.Immutable;
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
using Lemmy.Api;
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

    [AvaloniaTest]
    public async Task AFeedRowMarksAPostFromACommunityTheReaderFollows()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        services.Subscriptions.Record(new CommunityId(2), SubscriptionState.Subscribed);
        using FeedViewModel feed = await LoadedFeedAsync(services);

        Window window = Show(new FeedView(), feed);

        Assert.Multiple(() =>
        {
            Assert.That(feed.Posts[0].IsFromSubscribedCommunity, Is.True);
            Assert.That(VisibleText(window), Does.Contain("\u2713"));
        });
    }

    [AvaloniaTest]
    public async Task AFeedRowShowsNoMarkForACommunityTheReaderDoesNotFollow()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        using FeedViewModel feed = await LoadedFeedAsync(services);

        Window window = Show(new FeedView(), feed);

        Assert.Multiple(() =>
        {
            Assert.That(feed.Posts[0].IsFromSubscribedCommunity, Is.False);
            Assert.That(VisibleText(window), Does.Not.Contain("\u2713"));
        });
    }

    /// <summary>
    /// The reason the tracker exists: subscribing somewhere else has to reach the rows already on
    /// screen, which were told otherwise by the response that loaded them.
    /// </summary>
    [AvaloniaTest]
    public async Task SubscribingElsewhereMarksTheRowsAlreadyOnScreen()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        using FeedViewModel feed = await LoadedFeedAsync(services);
        Window window = Show(new FeedView(), feed);

        Assert.That(VisibleText(window), Does.Not.Contain("\u2713"), "nothing followed to begin with");

        services.Subscriptions.Record(new CommunityId(2), SubscriptionState.Subscribed);
        Settle();

        Assert.Multiple(() =>
        {
            Assert.That(feed.Posts[0].IsFromSubscribedCommunity, Is.True);
            Assert.That(VisibleText(window), Does.Contain("\u2713"));
        });
    }

    [AvaloniaTest]
    public async Task TheCommunityDirectoryOffersASubscribeButtonWhenSignedIn()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        services.Api.GetCommunitiesAsync(Arg.Any<CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(Sample.CommunitySummary()));

        using var directory = new CommunitiesViewModel(services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await directory.LoadAsync();

        Window window = Show(new CommunitiesView(), directory);

        Assert.That(VisibleText(window), Does.Contain("Subscribe"));
    }

    [AvaloniaTest]
    public async Task TheCommunityDirectoryHidesTheButtonWhenSignedOut()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(false);
        services.Api.GetCommunitiesAsync(Arg.Any<CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(Sample.CommunitySummary()));

        using var directory = new CommunitiesViewModel(services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await directory.LoadAsync();

        Window window = Show(new CommunitiesView(), directory);

        Assert.That(VisibleText(window), Does.Not.Contain("Subscribe"));
    }

    private static async Task<PostDetailViewModel> LoadedPostAsync(TestServices services)
    {
        services.Api.GetCommentsAsync(Arg.Any<CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommentThread([Sample.CommentNode()])));

        var page = new PostDetailViewModel(
            services.Services, new RecordingNavigator(), services.Api, Sample.PostSummary(), AppSettings.Default);
        await page.LoadAsync();
        return page;
    }

    [AvaloniaTest]
    public async Task SignedInThePostPageOffersABoxToComment()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        using PostDetailViewModel page = await LoadedPostAsync(services);

        Window window = Show(new PostDetailView(), page);

        Assert.Multiple(() =>
        {
            Assert.That(page.CanComment, Is.True);
            Assert.That(
                Descendants(window).OfType<CommentComposerView>().Any(view => view.IsEffectivelyVisible),
                Is.True);
            Assert.That(VisibleText(window), Does.Contain("Post"));
        });
    }

    [AvaloniaTest]
    public async Task SignedOutThereIsNoBoxToComment()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(false);
        using PostDetailViewModel page = await LoadedPostAsync(services);

        Window window = Show(new PostDetailView(), page);

        Assert.Multiple(() =>
        {
            Assert.That(page.CanComment, Is.False);
            Assert.That(
                Descendants(window).OfType<CommentComposerView>().Any(view => view.IsEffectivelyVisible),
                Is.False);
        });
    }

    [AvaloniaTest]
    public async Task TheReadersOwnCommentOffersEditAndDelete()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);

        // Sample comments are written by person 1.
        services.Account.Set(new Account(new PersonId(1), new Username("someone"), null, Sample.Instance, null));
        using PostDetailViewModel page = await LoadedPostAsync(services);

        Window window = Show(new PostDetailView(), page);

        Assert.That(VisibleText(window), Does.Contain("Edit").And.Contain("Delete").And.Contain("Reply"));
    }

    [AvaloniaTest]
    public async Task SomebodyElsesCommentOffersOnlyReply()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        services.Account.Set(new Account(new PersonId(99), new Username("stranger"), null, Sample.Instance, null));
        using PostDetailViewModel page = await LoadedPostAsync(services);

        Window window = Show(new PostDetailView(), page);

        Assert.Multiple(() =>
        {
            Assert.That(VisibleText(window), Does.Contain("Reply"));
            Assert.That(VisibleText(window), Does.Not.Contain("Delete"));
        });
    }

    [AvaloniaTest]
    public async Task PostingACommentPutsItAtTheTopOfTheThread()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        services.Api.CreateCommentAsync(
                Arg.Any<PostId>(), Arg.Any<CommentId?>(), Arg.Any<CommentDraft>(), Arg.Any<CancellationToken>())
            .Returns(Sample.CommentNode(id: 500, path: "0.500"));

        using PostDetailViewModel page = await LoadedPostAsync(services);
        Show(new PostDetailView(), page);

        page.Composer.Text = "Freshly written";
        await page.Composer.SubmitCommand.ExecuteAsync(null);
        Settle();

        Assert.Multiple(() =>
        {
            Assert.That(page.Comments, Has.Count.EqualTo(2));
            Assert.That(page.Comments[0].Node.Comment.Id, Is.EqualTo(new CommentId(500)));
        });
    }

    [AvaloniaTest]
    public async Task TheCommunityDirectoryOffersSubscribedOnceSignedIn()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        services.Api.GetCommunitiesAsync(Arg.Any<CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(Sample.CommunitySummary()));

        using var directory = new CommunitiesViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await directory.LoadAsync();

        Assert.That(
            directory.ListingOptions.Select(option => option.Value),
            Does.Contain(ListingType.Subscribed).And.Contain(ListingType.All).And.Contain(ListingType.Local));
    }

    [AvaloniaTest]
    public async Task TheCommunityDirectoryHasNoSubscribedListingWhenSignedOut()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(false);
        services.Api.GetCommunitiesAsync(Arg.Any<CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(Sample.CommunitySummary()));

        using var directory = new CommunitiesViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await directory.LoadAsync();

        Assert.That(
            directory.ListingOptions.Select(option => option.Value),
            Does.Not.Contain(ListingType.Subscribed));
    }

    [AvaloniaTest]
    public async Task ChoosingSubscribedAsksTheServerForSubscribedCommunities()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        services.Api.GetCommunitiesAsync(Arg.Any<CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(Sample.CommunitySummary()));

        using var directory = new CommunitiesViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await directory.LoadAsync();

        directory.SelectedListing = ListingType.Subscribed;
        await Task.Yield();

        await services.Api.Received().GetCommunitiesAsync(
            Arg.Is<CommunityQuery>(query => query.Listing == ListingType.Subscribed),
            Arg.Any<CancellationToken>());
    }

    [AvaloniaTest]
    public async Task SignedInTheDirectoryOpensOnTheReadersOwnCommunities()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        services.Api.GetCommunitiesAsync(Arg.Any<CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(Sample.CommunitySummary()));

        using var directory = new CommunitiesViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await directory.LoadAsync();

        Assert.That(directory.SelectedListing, Is.EqualTo(ListingType.Subscribed));
        await services.Api.Received().GetCommunitiesAsync(
            Arg.Is<CommunityQuery>(query => query.Listing == ListingType.Subscribed),
            Arg.Any<CancellationToken>());
    }

    [AvaloniaTest]
    public async Task SignedOutTheDirectoryOpensOnTheInstancesOwnCommunities()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(false);
        services.Api.GetCommunitiesAsync(Arg.Any<CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(Sample.CommunitySummary()));

        using var directory = new CommunitiesViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await directory.LoadAsync();

        Assert.That(directory.SelectedListing, Is.EqualTo(ListingType.Local));
    }

    /// <summary>
    /// The cost of opening on Subscribed: an account that follows nothing lands on an empty list,
    /// and a blank tab with no words on it reads as a broken one.
    /// </summary>
    [AvaloniaTest]
    public async Task AnAccountFollowingNothingIsToldSoRatherThanShownABlankTab()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        services.Api.GetCommunitiesAsync(Arg.Any<CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<CommunitySummary>.Empty);

        using var directory = new CommunitiesViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await directory.LoadAsync();

        Window window = Show(new CommunitiesView(), directory);

        Assert.Multiple(() =>
        {
            Assert.That(directory.HasNoCommunities, Is.True);
            Assert.That(
                VisibleText(window).Any(text => text.Contains("do not follow any communities", StringComparison.Ordinal)),
                Is.True);
        });
    }

    [AvaloniaTest]
    public async Task AnEmptyLocalListingSaysSomethingDifferent()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(false);
        services.Api.GetCommunitiesAsync(Arg.Any<CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<CommunitySummary>.Empty);

        using var directory = new CommunitiesViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await directory.LoadAsync();

        Window window = Show(new CommunitiesView(), directory);

        Assert.That(
            VisibleText(window).Any(text => text.Contains("No communities to show", StringComparison.Ordinal)),
            Is.True);
    }

    [AvaloniaTest]
    public async Task TheEmptyMessageGoesAwayOnceThereIsSomethingToShow()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        services.Api.GetCommunitiesAsync(Arg.Any<CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<CommunitySummary>.Empty);

        using var directory = new CommunitiesViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        await directory.LoadAsync();
        Assert.That(directory.HasNoCommunities, Is.True);

        services.Api.GetCommunitiesAsync(Arg.Any<CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(Sample.CommunitySummary()));
        await directory.ReloadAsync();

        Assert.That(directory.HasNoCommunities, Is.False);
    }



    [AvaloniaTest]
    public void TheServerPickerRendersItsList()
    {
        var services = new TestServices();
        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        shell.ToggleInstancePickerCommand.Execute(null);

        Window window = Show(new MainView(), shell);

        Assert.Multiple(() =>
        {
            Assert.That(VisibleText(window), Does.Contain("lemmy.ml"));
            Assert.That(VisibleText(window), Does.Contain("suggested"));
        });
    }

    [AvaloniaTest]
    public async Task TheProfilePageDrawsWhoTheAccountIs()
    {
        var services = new TestServices();
        using var page = new ProfileViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default, new PersonId(1));
        await page.LoadAsync();

        Window window = Show(new ProfileView(), page);

        Assert.Multiple(() =>
        {
            Assert.That(VisibleText(window), Does.Contain("alice"));
            Assert.That(
                VisibleText(window).Any(text => text.Contains("Reads more than posts", StringComparison.Ordinal)),
                Is.True,
                "the bio should render");
            Assert.That(VisibleText(window), Does.Contain("3 posts · 41 comments"));
        });
    }

    [AvaloniaTest]
    public async Task SigningOutIsOnTheReadersOwnPageAndNobodyElses()
    {
        var services = new TestServices();

        using var mine = new ProfileViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default,
            new PersonId(1), () => Task.CompletedTask);
        await mine.LoadAsync();

        using var theirs = new ProfileViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default, new PersonId(1));
        await theirs.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(VisibleText(Show(new ProfileView(), mine)), Does.Contain("Sign out"));
            Assert.That(VisibleText(Show(new ProfileView(), theirs)), Does.Not.Contain("Sign out"));
        });
    }

    [AvaloniaTest]
    public async Task TappingTheAccountNameOpensTheProfileRatherThanASheet()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        services.Api.GetMyAccountAsync(Arg.Any<CancellationToken>())
            .Returns(new Account(new PersonId(1), new Username("alice"), null, Sample.Instance, null));
        await services.SessionStore.SaveAsync(new StoredSession(
            AppSettings.Default.Instance, new SessionToken("jwt.token.value"), new Username("alice")));

        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        await shell.InitialiseAsync();

        shell.ToggleAccountCommand.Execute(null);
        Settle();

        Assert.That(shell.CurrentPage, Is.TypeOf<ProfileViewModel>());
    }
}
