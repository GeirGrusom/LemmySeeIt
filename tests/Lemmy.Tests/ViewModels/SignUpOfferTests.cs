using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// The sign-in sheet's offer to create an account. What matters is that it never invites somebody
/// to register somewhere that has shut its doors, and never guesses when it could not ask.
/// </summary>
[TestFixture]
internal sealed class SignUpOfferTests
{
    private static SiteSummary Site(
        RegistrationMode mode,
        bool requiresEmail = false) =>
        new(
            Sample.Instance,
            "Lemmy World",
            null,
            MarkdownText.Empty,
            null,
            null,
            "0.19.20",
            new VoteCount(10),
            new VoteCount(5),
            mode,
            MarkdownText.Empty,
            requiresEmail);

    private static async Task<MainViewModel> OpenSignInAsync(TestServices services, SiteSummary site)
    {
        services.Api.GetSiteAsync(Arg.Any<CancellationToken>()).Returns(site);

        var shell = new MainViewModel(services.Services, AppSettings.Default);
        shell.ToggleAccountCommand.Execute(null);

        // The lookup is deliberately not awaited by the command.
        await Task.Yield();
        return shell;
    }

    [Test]
    public async Task AnOpenInstanceIsOfferedForSignUp()
    {
        var services = new TestServices();
        using MainViewModel shell = await OpenSignInAsync(services, Site(RegistrationMode.Open));

        Assert.Multiple(() =>
        {
            Assert.That(shell.CanSignUp, Is.True);
            Assert.That(shell.SignUpNote, Does.Contain("open to new accounts"));
        });
    }

    [Test]
    public async Task AnInstanceWantingAnApplicationSaysSoBeforeTheReaderLeaves()
    {
        var services = new TestServices();
        using MainViewModel shell = await OpenSignInAsync(services, Site(RegistrationMode.RequireApplication));

        Assert.Multiple(() =>
        {
            Assert.That(shell.CanSignUp, Is.True);
            Assert.That(shell.SignUpNote, Does.Contain("approve"));
        });
    }

    [Test]
    public async Task AClosedInstanceIsNotOfferedAtAll()
    {
        var services = new TestServices();
        using MainViewModel shell = await OpenSignInAsync(services, Site(RegistrationMode.Closed));

        Assert.Multiple(() =>
        {
            Assert.That(shell.CanSignUp, Is.False);
            Assert.That(shell.SignUpNote, Does.Contain("not taking new accounts"));
        });
    }

    [Test]
    public async Task AnOpenInstanceThatWantsAnEmailSaysThatToo()
    {
        var services = new TestServices();
        using MainViewModel shell = await OpenSignInAsync(services, Site(RegistrationMode.Open, requiresEmail: true));

        Assert.That(shell.SignUpNote, Does.Contain("email"));
    }

    [Test]
    public async Task AnInstanceThatCouldNotBeAskedIsNotGuessedAt()
    {
        var services = new TestServices();
        services.Api.GetSiteAsync(Arg.Any<CancellationToken>())
            .Returns<SiteSummary>(_ => throw new LemmyApiException("Could not reach lemmy.world."));

        var shell = new MainViewModel(services.Services, AppSettings.Default);
        shell.ToggleAccountCommand.Execute(null);
        await Task.Yield();

        using (shell)
        {
            Assert.Multiple(() =>
            {
                Assert.That(shell.CanSignUp, Is.False);
                Assert.That(shell.SignUpNote, Is.Null, "silence beats a guess about somebody else's server");
            });
        }
    }

    [Test]
    public async Task SigningUpOpensTheInstancesOwnPage()
    {
        var services = new TestServices();
        using MainViewModel shell = await OpenSignInAsync(services, Site(RegistrationMode.Open));

        await shell.SignUpCommand.ExecuteAsync(null);

        await services.LinkOpener.Received(1).OpenAsync(
            Arg.Is<WebLink>(link => link.Value == "https://lemmy.world/signup"));
    }

    [TestCase("Open", RegistrationMode.Open)]
    [TestCase("RequireApplication", RegistrationMode.RequireApplication)]
    [TestCase("Closed", RegistrationMode.Closed)]
    [TestCase("SomethingNewer", RegistrationMode.Unknown)]
    [TestCase(null, RegistrationMode.Unknown)]
    public void TheWireValueIsRead(string? wire, RegistrationMode expected) =>
        Assert.That(wire.ToRegistrationMode(), Is.EqualTo(expected));

    [Test]
    public void AModeThisClientDoesNotKnowIsWorthTrying()
    {
        // A newer Lemmy could name a mode we have never heard of; hiding the link would be worse.
        Assert.Multiple(() =>
        {
            Assert.That(RegistrationMode.Unknown.AcceptsNewAccounts(), Is.True);
            Assert.That(RegistrationMode.Closed.AcceptsNewAccounts(), Is.False);
        });
    }
}
