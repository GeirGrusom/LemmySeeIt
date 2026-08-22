using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// Whether content flagged not safe for work is shown. Two halves: a switch the reader can reach,
/// and honouring the setting they already made on their instance.
/// </summary>
[TestFixture]
internal sealed class ContentSettingsTests
{
    private TestServices services = null!;

    [SetUp]
    public void CreateServices()
    {
        services = new TestServices();
        services.FeedReturns(Sample.PostPage(2, null));
    }

    private MainViewModel CreateShell() => new(services.Services, AppSettings.Default);

    private static Account Alice(bool showNsfw, bool blurNsfw = true) =>
        new(new PersonId(42), new Username("alice"), null, InstanceAddress.Parse("lemmy.world"), null, showNsfw, blurNsfw);

    private void SignInReturns(Account account)
    {
        services.Api.LogInAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SessionToken("issued")));
        services.Api.GetMyAccountAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<Account?>(account));
        services.Api.IsAuthenticated.Returns(true);
    }

    [Test]
    public async Task ItStartsOffHiddenAsBefore()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        Assert.Multiple(() =>
        {
            Assert.That(shell.ShowNsfw, Is.False);
            Assert.That(shell.BlurNsfwImages, Is.True);
        });
    }

    /// <summary>The switch has to reach the request; filtering after the fact would show nothing.</summary>
    [Test]
    public async Task TurningItOnAsksTheServerForIt()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        shell.ShowNsfw = true;
        await Task.Yield();

        await services.Api.Received().GetFeedAsync(
            Arg.Is<FeedQuery>(query => query.ShowNsfw),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TurningItOnIsRemembered()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        shell.ShowNsfw = true;
        await Task.Yield();

        await services.SettingsStore.Received().SaveAsync(
            Arg.Is<AppSettings>(settings => settings.ShowNsfw),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TheBlurSettingIsRememberedToo()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        shell.BlurNsfwImages = false;
        await Task.Yield();

        await services.SettingsStore.Received().SaveAsync(
            Arg.Is<AppSettings>(settings => !settings.BlurNsfwImages),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TheCommunityDirectoryAsksForThemToo()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.ShowNsfw = true;
        await Task.Yield();

        await shell.ShowSectionAsync(AppSection.Communities);

        await services.Api.Received().GetCommunitiesAsync(
            Arg.Is<CommunityQuery>(query => query.ShowNsfw),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The half that was actually broken: someone who turned this on through their instance's own
    /// settings still saw nothing here, because the app overrode it with its own default.
    /// </summary>
    [Test]
    public async Task SigningInAdoptsWhatTheAccountAlreadyChose()
    {
        SignInReturns(Alice(showNsfw: true, blurNsfw: false));
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.SignInName = "alice";
        shell.SignInPassword = "hunter2";

        await shell.SignInCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(shell.ShowNsfw, Is.True);
            Assert.That(shell.BlurNsfwImages, Is.False);
        });
    }

    [Test]
    public async Task AnAccountThatWantsItHiddenLeavesItHidden()
    {
        SignInReturns(Alice(showNsfw: false));
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.SignInName = "alice";
        shell.SignInPassword = "hunter2";

        await shell.SignInCommand.ExecuteAsync(null);

        Assert.That(shell.ShowNsfw, Is.False);
    }

    [Test]
    public async Task WhatTheAccountChoseIsRememberedForNextLaunch()
    {
        SignInReturns(Alice(showNsfw: true));
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.SignInName = "alice";
        shell.SignInPassword = "hunter2";

        await shell.SignInCommand.ExecuteAsync(null);

        await services.SettingsStore.Received().SaveAsync(
            Arg.Is<AppSettings>(settings => settings.ShowNsfw),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RestoringASessionAdoptsTheAccountsSettingsAsWell()
    {
        await services.SessionStore.SaveAsync(
            new StoredSession(AppSettings.Default.Instance, new SessionToken("stored"), new Username("alice")));
        services.Api.GetMyAccountAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Account?>(Alice(showNsfw: true)));

        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        Assert.That(shell.ShowNsfw, Is.True);
    }

    /// <summary>Loading saved settings is not the reader flicking a switch, so nothing is refetched.</summary>
    [Test]
    public async Task LoadingSavedSettingsDoesNotCountAsAChange()
    {
        services.SettingsStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(AppSettings.Default with { ShowNsfw = true }));

        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(shell.ShowNsfw, Is.True);
            await services.SettingsStore.DidNotReceive().SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task SettingItToWhatItAlreadyIsChangesNothing()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        shell.ShowNsfw = false;
        await Task.Yield();

        await services.SettingsStore.DidNotReceive().SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
    }
}
