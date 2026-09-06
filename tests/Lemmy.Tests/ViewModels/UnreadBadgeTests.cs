using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>The count beside the account name in the header, and what keeps it honest.</summary>
[TestFixture]
internal sealed class UnreadBadgeTests
{
    private TestServices services = null!;

    [SetUp]
    public void CreateServices()
    {
        services = new TestServices();
        services.FeedReturns(Sample.PostPage(2));
    }

    /// <summary>Puts a restored session in place, as a launch with somebody already signed in has.</summary>
    private async Task SignedInAsync(UnreadTally waiting)
    {
        services.Api.IsAuthenticated.Returns(true);
        services.Api.GetMyAccountAsync(Arg.Any<CancellationToken>())
            .Returns(new Account(new PersonId(1), new Username("alice"), null, Sample.Instance, null));
        services.Api.GetUnreadCountAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(waiting));
        await services.SessionStore.SaveAsync(new StoredSession(
            AppSettings.Default.Instance, new SessionToken("jwt.token.value"), new Username("alice")));
    }

    private MainViewModel CreateShell() => new(services.Services, AppSettings.Default);

    [Test]
    public async Task ALaunchWithNobodySignedInShowsNoBadge()
    {
        using MainViewModel shell = CreateShell();

        await shell.InitialiseAsync();

        Assert.That(shell.HasUnread, Is.False);
    }

    [Test]
    public async Task ALaunchWithSomethingWaitingShowsTheCount()
    {
        await SignedInAsync(new UnreadTally(3, 2, 9));
        using MainViewModel shell = CreateShell();

        await shell.InitialiseAsync();

        Assert.Multiple(() =>
        {
            Assert.That(shell.HasUnread, Is.True);
            Assert.That(shell.UnreadLabel, Is.EqualTo("5"), "the nine messages are not counted");
        });
    }

    /// <summary>A nought beside the account name is a control that says nothing.</summary>
    [Test]
    public async Task NothingWaitingShowsNoBadgeEvenWhenSignedIn()
    {
        await SignedInAsync(UnreadTally.None);
        using MainViewModel shell = CreateShell();

        await shell.InitialiseAsync();

        Assert.That(shell.HasUnread, Is.False);
    }

    /// <summary>The badge is not worth an error on the screen; the launch carries on without it.</summary>
    [Test]
    public async Task AFailedCountDoesNotFailTheLaunch()
    {
        await SignedInAsync(UnreadTally.None);
        services.Api.GetUnreadCountAsync(Arg.Any<CancellationToken>())
            .Returns<UnreadTally>(_ => throw new LemmyApiException("lemmy.world answered 502."));
        using MainViewModel shell = CreateShell();

        await shell.InitialiseAsync();

        Assert.Multiple(() =>
        {
            Assert.That(shell.CurrentPage, Is.TypeOf<FeedViewModel>());
            Assert.That(shell.HasUnread, Is.False);
        });
    }

    [Test]
    public async Task TheBadgeFollowsTheSharedCounterWhereverItIsChanged()
    {
        await SignedInAsync(new UnreadTally(1, 0, 0));
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        services.Unread.Set(new UnreadTally(4, 0, 0));

        Assert.That(shell.UnreadLabel, Is.EqualTo("4"));
    }

    [Test]
    public async Task OpeningTheBadgeShowsTheNotifications()
    {
        await SignedInAsync(new UnreadTally(2, 0, 0));
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        shell.ShowNotificationsCommand.Execute(null);

        Assert.That(shell.CurrentPage, Is.TypeOf<NotificationsViewModel>());
    }

    /// <summary>
    /// The list only ever showed a page of what is waiting, so leaving it asks the server again
    /// rather than trusting what was counted on screen.
    /// </summary>
    [Test]
    public async Task LeavingTheListAsksTheServerAgain()
    {
        await SignedInAsync(new UnreadTally(2, 0, 0));
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();
        shell.ShowNotificationsCommand.Execute(null);
        services.Api.ClearReceivedCalls();

        shell.Pop();

        await services.Api.Received(1).GetUnreadCountAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SigningOutTakesTheBadgeWithIt()
    {
        await SignedInAsync(new UnreadTally(6, 0, 0));
        using MainViewModel shell = CreateShell();
        await shell.InitialiseAsync();

        Assert.That(shell.HasUnread, Is.True);

        await shell.SignOutAsync();

        Assert.Multiple(() =>
        {
            Assert.That(shell.HasUnread, Is.False);
            Assert.That(services.Unread.Tally.Any, Is.False);
        });
    }
}
