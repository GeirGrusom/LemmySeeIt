using System.Collections.Immutable;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// Choosing a server from the picker. Plain tests rather than Avalonia ones: none of this needs a
/// window, and running substitute-heavy view-model tests on the shared headless UI thread leaves
/// NSubstitute's thread-local matcher state to leak between them.
/// </summary>
[TestFixture]
internal sealed class InstancePickerTests
{

    [Test]
    public void TheServerPickerOffersTheSuggestionsAndMarksTheCurrentOne()
    {
        var services = new TestServices();
        using var shell = new MainViewModel(services.Services, AppSettings.Default);

        shell.ToggleInstancePickerCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(shell.InstanceChoices, Is.Not.Empty);
            Assert.That(shell.InstanceChoices[0].Address, Is.EqualTo(AppSettings.Default.Instance));
            Assert.That(shell.InstanceChoices[0].IsCurrent, Is.True);
            Assert.That(
                shell.InstanceChoices.Select(choice => choice.Label),
                Does.Contain("lemmy.ml").And.Contain("sopuli.xyz"));
            Assert.That(shell.InstanceChoices.Select(choice => choice.Address), Is.Unique);
        });
    }

    [Test]
    public void AServerAlreadyUsedIsNotRepeatedAmongTheSuggestions()
    {
        var services = new TestServices();
        AppSettings used = AppSettings.Default.WithInstance(InstanceAddress.Parse("lemmy.ml"));
        using var shell = new MainViewModel(services.Services, used);

        shell.ToggleInstancePickerCommand.Execute(null);

        InstanceChoice[] matches = [.. shell.InstanceChoices.Where(choice => choice.Label == "lemmy.ml")];

        Assert.Multiple(() =>
        {
            Assert.That(matches, Has.Length.EqualTo(1));
            Assert.That(matches[0].IsRemembered, Is.True, "it should read as one the reader has used");
        });
    }

    /// <summary>
    /// A tap in a scrolling list must not be able to end a session. Signed in, choosing another
    /// server asks first; signed out it just moves.
    /// </summary>
    [Test]
    public async Task SignedInChoosingAnotherServerAsksBeforeSigningOut()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        services.Api.GetMyAccountAsync(Arg.Any<CancellationToken>())
            .Returns(new Account(new PersonId(1), new Username("someone"), null, AppSettings.Default.Instance, null));
        // A real MemorySessionStore, so it is seeded rather than stubbed.
        await services.SessionStore.SaveAsync(new StoredSession(
            AppSettings.Default.Instance, new SessionToken("jwt.token.value"), new Username("someone")));

        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        await shell.InitialiseAsync();
        shell.ToggleInstancePickerCommand.Execute(null);

        InstanceChoice elsewhere = shell.InstanceChoices.First(choice => !choice.IsCurrent);
        await shell.ChooseInstanceCommand.ExecuteAsync(elsewhere);

        Assert.Multiple(() =>
        {
            Assert.That(shell.IsSignedIn, Is.True, "nothing should have happened yet");
            Assert.That(shell.HasPendingInstance, Is.True);
            Assert.That(shell.PendingInstanceWarning, Does.Contain("signs you out"));
            Assert.That(shell.InstanceLabel, Is.EqualTo(AppSettings.Default.Instance.Value), "still on the old server");
        });

        shell.CancelInstanceChangeCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(shell.HasPendingInstance, Is.False);
            Assert.That(shell.IsSignedIn, Is.True, "declining must leave the session alone");
        });
    }

    [Test]
    public async Task SignedOutChoosingAnotherServerJustMoves()
    {
        var services = new TestServices();
        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        shell.ToggleInstancePickerCommand.Execute(null);

        InstanceChoice elsewhere = shell.InstanceChoices.First(choice => !choice.IsCurrent);
        await shell.ChooseInstanceCommand.ExecuteAsync(elsewhere);

        Assert.Multiple(() =>
        {
            Assert.That(shell.HasPendingInstance, Is.False, "there is no session to ask about");
            Assert.That(shell.InstanceLabel, Is.EqualTo(elsewhere.Label));
        });
    }

    [Test]
    public async Task ChoosingTheServerAlreadyInUseJustClosesTheSheet()
    {
        var services = new TestServices();
        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        shell.ToggleInstancePickerCommand.Execute(null);

        await shell.ChooseInstanceCommand.ExecuteAsync(shell.InstanceChoices[0]);

        Assert.Multiple(() =>
        {
            Assert.That(shell.IsInstancePickerOpen, Is.False);
            Assert.That(shell.HasPendingInstance, Is.False);
        });
    }

    [Test]
    public async Task AServerSwitchedToIsRememberedForNextTime()
    {
        var services = new TestServices();
        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        shell.ToggleInstancePickerCommand.Execute(null);

        InstanceChoice elsewhere = shell.InstanceChoices.First(choice => !choice.IsCurrent);
        await shell.ChooseInstanceCommand.ExecuteAsync(elsewhere);

        await services.SettingsStore.Received().SaveAsync(
            Arg.Is<AppSettings>(saved => saved.Recent.Contains(elsewhere.Address)),
            Arg.Any<CancellationToken>());
    }
}
