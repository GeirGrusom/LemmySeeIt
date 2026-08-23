using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.Session;

/// <summary>Signing in and out from the shell, and what it changes.</summary>
[TestFixture]
internal sealed class ShellSessionTests
{
    private TestServices services = null!;

    [SetUp]
    public void CreateServices()
    {
        services = new TestServices();
        services.FeedReturns(Sample.PostPage(2, null));
    }

    private static Account Alice(string instance = "lemmy.world") =>
        new(new PersonId(42), new Username("alice"), "Alice", InstanceAddress.Parse(instance), null);

    private void SignInSucceeds(string token = "issued-token")
    {
        services.Api.LogInAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SessionToken(token)));
        services.Api.GetMyAccountAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<Account?>(Alice()));
        services.Api.IsAuthenticated.Returns(true);
    }

    private MainViewModel CreateShell() => new(services.Services, AppSettings.Default);

    [Test]
    public async Task NobodyIsSignedInToBeginWith()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        Assert.Multiple(() =>
        {
            Assert.That(shell.IsSignedIn, Is.False);
            Assert.That(shell.AccountLabel, Is.EqualTo("Sign in"));
        });
    }

    [Test]
    public async Task SigningInRemembersTheAccountAndTheSession()
    {
        SignInSucceeds();
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.SignInName = "alice";
        shell.SignInPassword = "hunter2";

        await shell.SignInCommand.ExecuteAsync(null);

        StoredSession? stored = await services.SessionStore.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(shell.IsSignedIn, Is.True);
            Assert.That(shell.AccountLabel, Is.EqualTo("Alice"));
            Assert.That(shell.IsSignInOpen, Is.False);
            Assert.That(stored?.Token.Value, Is.EqualTo("issued-token"));
            Assert.That(stored?.AccountName.Value, Is.EqualTo("alice"));
        });
    }

    /// <summary>The password exists for one request and is not kept anywhere afterwards.</summary>
    [Test]
    public async Task ThePasswordIsDiscardedImmediately()
    {
        SignInSucceeds();
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.SignInName = "alice";
        shell.SignInPassword = "hunter2";
        shell.SignInTotp = "123456";

        await shell.SignInCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(shell.SignInPassword, Is.Empty);
            Assert.That(shell.SignInTotp, Is.Empty);
        });
    }

    [Test]
    public async Task ThePasswordIsAlsoDiscardedWhenSignInFails()
    {
        services.Api.LogInAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<SessionToken>>(_ => throw new LemmyApiException("lemmy.world answered 401 (incorrect_login)."));
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.ToggleAccountCommand.Execute(null);
        shell.SignInName = "alice";
        shell.SignInPassword = "wrong";

        await shell.SignInCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(shell.SignInPassword, Is.Empty);
            Assert.That(shell.SignInError, Does.Contain("incorrect_login"));
            Assert.That(shell.IsSignedIn, Is.False);
            Assert.That(shell.IsSignInOpen, Is.True, "the sheet stays open so it can be tried again");
        });
    }

    [Test]
    public async Task SignInNeedsBothFields()
    {
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.SignInName = "alice";

        await shell.SignInCommand.ExecuteAsync(null);

        Assert.Multiple(async () =>
        {
            Assert.That(shell.SignInError, Is.Not.Null);
            await services.Api.DidNotReceive().LogInAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>());
        });
    }

    /// <summary>
    /// Signing out has to reach the server. Lemmy tokens never expire, so one merely forgotten
    /// locally stays valid for anyone who has a copy.
    /// </summary>
    [Test]
    public async Task SigningOutTellsTheInstanceAndForgetsTheSession()
    {
        SignInSucceeds();
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.SignInName = "alice";
        shell.SignInPassword = "hunter2";
        await shell.SignInCommand.ExecuteAsync(null);

        await shell.SignOutAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(shell.IsSignedIn, Is.False);
            Assert.That(await services.SessionStore.LoadAsync(), Is.Null);
            await services.Api.Received().LogOutAsync(Arg.Any<CancellationToken>());
        });
    }

    /// <summary>
    /// The header control must not sign anyone out on its own. Signing out cannot be undone — it
    /// invalidates the token server-side — and the control sits in a header that shifts as the
    /// account name replaces "Sign in", so a stray tap lands on it easily. It did, during testing.
    /// </summary>
    [Test]
    public async Task TappingTheAccountControlDoesNotSignOut()
    {
        SignInSucceeds();
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.SignInName = "alice";
        shell.SignInPassword = "hunter2";
        await shell.SignInCommand.ExecuteAsync(null);

        shell.ToggleAccountCommand.Execute(null);

        Assert.Multiple(async () =>
        {
            Assert.That(shell.IsSignedIn, Is.True, "it opens the account's page instead");
            Assert.That(shell.CurrentPage, Is.TypeOf<ProfileViewModel>());
            await services.Api.DidNotReceive().LogOutAsync(Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task SigningOutHappensOnlyWhenAskedForExplicitly()
    {
        SignInSucceeds();
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.SignInName = "alice";
        shell.SignInPassword = "hunter2";
        await shell.SignInCommand.ExecuteAsync(null);
        shell.ToggleAccountCommand.Execute(null);

        var profile = (ProfileViewModel)shell.CurrentPage!;
        await profile.SignOutCommand.ExecuteAsync(null);

        Assert.Multiple(async () =>
        {
            Assert.That(shell.IsSignedIn, Is.False);
            await services.Api.Received().LogOutAsync(Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task LeavingTheAccountPageChangesNothing()
    {
        SignInSucceeds();
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.SignInName = "alice";
        shell.SignInPassword = "hunter2";
        await shell.SignInCommand.ExecuteAsync(null);
        shell.ToggleAccountCommand.Execute(null);

        shell.Pop();

        Assert.Multiple(() =>
        {
            Assert.That(shell.CurrentPage, Is.Not.TypeOf<ProfileViewModel>());
            Assert.That(shell.IsSignedIn, Is.True);
        });
    }

    [Test]
    public async Task AStoredSessionIsPickedUpAtLaunch()
    {
        await services.SessionStore.SaveAsync(
            new StoredSession(AppSettings.Default.Instance, new SessionToken("stored"), new Username("alice")));
        services.Api.GetMyAccountAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<Account?>(Alice()));

        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        Assert.That(shell.IsSignedIn, Is.True);
    }

    /// <summary>A stored token that no longer works must not leave the app looking signed in.</summary>
    [Test]
    public async Task AStoredSessionThatNoLongerWorksIsDiscarded()
    {
        await services.SessionStore.SaveAsync(
            new StoredSession(AppSettings.Default.Instance, new SessionToken("stale"), new Username("alice")));
        services.Api.GetMyAccountAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<Account?>(null));

        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(shell.IsSignedIn, Is.False);
            Assert.That(await services.SessionStore.LoadAsync(), Is.Null, "and it is not left lying around");
        });
    }

    /// <summary>A session belongs to one server, so a stored one for elsewhere is not used here.</summary>
    [Test]
    public async Task ASessionForADifferentInstanceIsNotUsed()
    {
        await services.SessionStore.SaveAsync(
            new StoredSession(InstanceAddress.Parse("sh.itjust.works"), new SessionToken("elsewhere"), new Username("alice")));

        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(shell.IsSignedIn, Is.False);
            await services.Api.DidNotReceive().GetMyAccountAsync(Arg.Any<CancellationToken>());
        });
    }

    /// <summary>Being offline at launch should not throw away a session that is probably still fine.</summary>
    [Test]
    public async Task BeingOfflineAtLaunchKeepsTheSession()
    {
        await services.SessionStore.SaveAsync(
            new StoredSession(AppSettings.Default.Instance, new SessionToken("stored"), new Username("alice")));
        services.Api.GetMyAccountAsync(Arg.Any<CancellationToken>())
            .Returns<Task<Account?>>(_ => throw new LemmyApiException("Could not reach lemmy.world."));

        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(shell.IsSignedIn, Is.True);
            Assert.That(shell.AccountLabel, Is.EqualTo("alice"));
            Assert.That(await services.SessionStore.LoadAsync(), Is.Not.Null);
        });
    }

    [Test]
    public async Task SwitchingInstanceSignsOutOfTheOldOne()
    {
        SignInSucceeds();
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.SignInName = "alice";
        shell.SignInPassword = "hunter2";
        await shell.SignInCommand.ExecuteAsync(null);

        shell.InstanceInput = "sh.itjust.works";
        await shell.ApplyInstanceCommand.ExecuteAsync(null);

        Assert.Multiple(async () =>
        {
            Assert.That(shell.IsSignedIn, Is.False);
            Assert.That(await services.SessionStore.LoadAsync(), Is.Null);
            await services.Api.Received().LogOutAsync(Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task TheSubscribedListingIsOnlyOfferedOnceSignedIn()
    {
        // Read each list before changing the substitute: the property is computed on access, and
        // the feed is rebuilt on sign-in anyway, so it is only ever read against one answer.
        services.Api.IsAuthenticated.Returns(false);
        using var anonymous = new FeedViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        ListingType[] anonymousOptions = [.. anonymous.ListingOptions.Select(option => option.Value)];

        services.Api.IsAuthenticated.Returns(true);
        using var signedIn = new FeedViewModel(
            services.Services, new RecordingNavigator(), services.Api, AppSettings.Default);
        ListingType[] signedInOptions = [.. signedIn.ListingOptions.Select(option => option.Value)];

        await Task.CompletedTask;

        Assert.Multiple(() =>
        {
            Assert.That(anonymousOptions, Does.Not.Contain(ListingType.Subscribed));
            Assert.That(signedInOptions, Does.Contain(ListingType.Subscribed));
        });
    }

    /// <summary>
    /// A reader who chose Subscribed and later signed out would otherwise get an empty feed with no
    /// explanation for it.
    /// </summary>
    [Test]
    public async Task ASavedSubscribedListingFallsBackWhenSignedOut()
    {
        services.Api.IsAuthenticated.Returns(false);
        using var feed = new FeedViewModel(
            services.Services,
            new RecordingNavigator(),
            services.Api,
            AppSettings.Default with { Listing = ListingType.Subscribed });

        await feed.LoadAsync();

        Assert.That(feed.SelectedListing, Is.EqualTo(ListingType.All));
    }
}
