using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

[TestFixture]
internal sealed class MainViewModelTests
{
    private TestServices services = null!;

    [SetUp]
    public void CreateServices()
    {
        services = new TestServices();
        services.FeedReturns(Sample.PostPage(2));
        services.Api.GetCommunitiesAsync(Arg.Any<Lemmy.Api.CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(System.Collections.Immutable.ImmutableArray.Create(Sample.CommunitySummary())));
    }

    private MainViewModel CreateShell() => new(services.Services, AppSettings.Default);

    [Test]
    public async Task InitialiseAsync_LoadsSettingsAndShowsTheFeed()
    {
        using MainViewModel shell = CreateShell();

        await shell.InitialiseAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(shell.CurrentPage, Is.TypeOf<FeedViewModel>());
            Assert.That(shell.CanPop, Is.False);
            Assert.That(shell.InstanceLabel, Is.EqualTo("lemmy.world"));
            await services.SettingsStore.Received(1).LoadAsync(Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task Push_ThenPop_ReturnsToWhereTheReaderWas()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        PageViewModel? feed = shell.CurrentPage;

        shell.Push(new SearchViewModel(services.Services, shell, services.Api, AppSettings.Default));

        Assert.That(shell.CanPop, Is.True);

        shell.Pop();

        Assert.Multiple(() =>
        {
            Assert.That(shell.CurrentPage, Is.SameAs(feed));
            Assert.That(shell.CanPop, Is.False);
        });
    }

    [Test]
    public async Task Pop_AtTheRootDoesNothing()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        PageViewModel? feed = shell.CurrentPage;

        shell.Pop();

        Assert.That(shell.CurrentPage, Is.SameAs(feed));
    }

    /// <summary>
    /// Switching away and back should not re-fetch: the reader's scroll position and loaded pages
    /// are the whole reason a section keeps its own root.
    /// </summary>
    [Test]
    public async Task SwitchingSectionsAndBack_KeepsTheOriginalPageAlive()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        PageViewModel? feed = shell.CurrentPage;

        await shell.ShowSectionAsync(AppSection.Communities);
        await shell.ShowSectionAsync(AppSection.Feed);

        Assert.Multiple(async () =>
        {
            Assert.That(shell.CurrentPage, Is.SameAs(feed));
            await services.Api.Received(1).GetFeedAsync(Arg.Any<Lemmy.Api.FeedQuery>(), Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task ShowSectionAsync_CreatesTheRightRootForEachSection()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        await shell.ShowSectionAsync(AppSection.Communities);
        Assert.That(shell.CurrentPage, Is.TypeOf<CommunitiesViewModel>());

        await shell.ShowSectionAsync(AppSection.Search);
        Assert.That(shell.CurrentPage, Is.TypeOf<SearchViewModel>());
    }

    [Test]
    public async Task SwitchingSection_DropsTheBackStack()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.Push(new SearchViewModel(services.Services, shell, services.Api, AppSettings.Default));

        await shell.ShowSectionAsync(AppSection.Communities);

        Assert.That(shell.CanPop, Is.False);
    }

    [Test]
    public async Task ToggleInstancePicker_OpensItPrimedWithTheCurrentInstance()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        shell.ToggleInstancePickerCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(shell.IsInstancePickerOpen, Is.True);
            Assert.That(shell.InstanceInput, Is.EqualTo("lemmy.world"));
            Assert.That(shell.InstanceError, Is.Null);
        });
    }

    [Test]
    public async Task ApplyInstance_RejectsSomethingThatIsNotAHostAndStaysPut()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.InstanceInput = "not a host";

        shell.ApplyInstanceCommand.Execute(null);

        Assert.Multiple(async () =>
        {
            Assert.That(shell.InstanceError, Is.Not.Null);
            Assert.That(shell.InstanceLabel, Is.EqualTo("lemmy.world"));
            await services.SettingsStore.DidNotReceive().SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task ApplyInstance_SwitchesServersAndSavesTheChoice()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.InstanceInput = "https://sh.itjust.works/c/asklemmy";

        await shell.ApplyInstanceCommand.ExecuteAsync(null);

        Assert.Multiple(async () =>
        {
            Assert.That(shell.InstanceLabel, Is.EqualTo("sh.itjust.works"));
            Assert.That(shell.IsInstancePickerOpen, Is.False);
            Assert.That(shell.CurrentPage, Is.TypeOf<FeedViewModel>());
            await services.SettingsStore.Received(1).SaveAsync(
                Arg.Is<AppSettings>(settings => settings.Instance == InstanceAddress.Parse("sh.itjust.works")),
                Arg.Any<CancellationToken>());
            services.ApiFactory.Received().Create(InstanceAddress.Parse("sh.itjust.works"));
        });
    }

    /// <summary>
    /// Post and community identifiers mean different things on different servers, so nothing loaded
    /// from the old instance may survive the switch.
    /// </summary>
    [Test]
    public async Task ApplyInstance_DiscardsEveryPageFromThePreviousServer()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        PageViewModel? original = shell.CurrentPage;
        shell.InstanceInput = "sh.itjust.works";

        await shell.ApplyInstanceCommand.ExecuteAsync(null);

        Assert.That(shell.CurrentPage, Is.Not.SameAs(original));
    }

    [Test]
    public async Task ApplyInstance_ToTheSameServerJustClosesThePicker()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        PageViewModel? original = shell.CurrentPage;
        shell.ToggleInstancePickerCommand.Execute(null);

        await shell.ApplyInstanceCommand.ExecuteAsync(null);

        Assert.Multiple(async () =>
        {
            Assert.That(shell.IsInstancePickerOpen, Is.False);
            Assert.That(shell.CurrentPage, Is.SameAs(original));
            await services.SettingsStore.DidNotReceive().SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task PageTitle_FollowsTheCurrentPage()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        Assert.That(shell.PageTitle, Is.EqualTo("Front page"));

        await shell.ShowSectionAsync(AppSection.Search);

        Assert.That(shell.PageTitle, Is.EqualTo("Search"));
    }

    /// <summary>
    /// Android's back gesture is the main way people navigate there, so the shell has to answer it
    /// the way the platform expects: unwind the page stack first.
    /// </summary>
    [Test]
    public async Task TryGoBack_UnwindsThePageStackFirst()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        PageViewModel? feed = shell.CurrentPage;
        shell.Push(new SearchViewModel(services.Services, shell, services.Api, AppSettings.Default));

        bool handled = shell.TryGoBack();

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(shell.CurrentPage, Is.SameAs(feed));
        });
    }

    [Test]
    public async Task TryGoBack_ReturnsToTheFeedFromAnotherSectionsRoot()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        await shell.ShowSectionAsync(AppSection.Communities);
        shell.SelectedSection = AppSection.Communities;

        bool handled = shell.TryGoBack();

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(shell.SelectedSection, Is.EqualTo(AppSection.Feed));
        });
    }

    /// <summary>
    /// Reporting "not handled" at the root is the point: it is how Android gets to background the
    /// app. Swallowing it would trap the reader inside a client they cannot back out of.
    /// </summary>
    [Test]
    public async Task TryGoBack_DoesNotHandleTheRequestAtTheFeedRoot()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        Assert.That(shell.TryGoBack(), Is.False);
    }

    [Test]
    public async Task Dispose_IsSafeToCallTwice()
    {
        MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        shell.Dispose();

        Assert.That(shell.Dispose, Throws.Nothing);
    }
}
