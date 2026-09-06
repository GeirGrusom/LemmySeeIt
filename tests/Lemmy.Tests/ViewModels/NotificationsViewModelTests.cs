using System.Collections.Immutable;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>The list of what people have said to the signed-in account, and the badge above it.</summary>
[TestFixture]
internal sealed class NotificationsViewModelTests
{
    private TestServices services = null!;
    private RecordingNavigator navigator = null!;

    [SetUp]
    public void CreateServices()
    {
        services = new TestServices();
        navigator = new RecordingNavigator();
    }

    private void NotificationsAre(params Notification[] notifications) =>
        services.Api.GetNotificationsAsync(Arg.Any<NotificationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ImmutableArray.Create(notifications)));

    private void CounterSays(UnreadTally tally) =>
        services.Api.GetUnreadCountAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(tally));

    private NotificationsViewModel CreatePage() =>
        new(services.Services, navigator, services.Api, AppSettings.Default);

    [Test]
    public async Task LoadAsync_ShowsWhatCameBack()
    {
        NotificationsAre(
            Sample.Notification(7),
            Sample.Notification(9, NotificationKind.Mention, isRead: true));
        using NotificationsViewModel page = CreatePage();

        await page.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.Notifications, Has.Count.EqualTo(2));
            Assert.That(page.Notifications[0].IsUnread, Is.True);
            Assert.That(page.Notifications[1].KindLabel, Is.EqualTo("mentioned you"));
            Assert.That(page.HasNothing, Is.False);
        });
    }

    [Test]
    public async Task AnEmptyListSaysSoDifferentlyDependingOnTheFilter()
    {
        using NotificationsViewModel page = CreatePage();

        await page.LoadAsync();
        string unread = page.NothingLabel;

        await page.ToggleFilterCommand.ExecuteAsync(null);
        string everything = page.NothingLabel;

        Assert.Multiple(() =>
        {
            Assert.That(page.HasNothing, Is.True);
            Assert.That(unread, Does.Contain("Nothing unread"));
            Assert.That(everything, Does.Contain("Nobody has replied"));
        });
    }

    /// <summary>
    /// The badge follows the server's count, not the rows on screen: this is one page of a possibly
    /// longer list, and counting what arrived would shrink a badge that is telling the truth.
    /// </summary>
    [Test]
    public async Task TheBadgeTakesTheServersCountRatherThanCountingTheRows()
    {
        NotificationsAre(Sample.Notification(7));
        CounterSays(new UnreadTally(40, 2, 0));
        using NotificationsViewModel page = CreatePage();

        await page.LoadAsync();

        Assert.That(services.Unread.Tally.Total, Is.EqualTo(42));
    }

    [Test]
    public async Task MarkingOneReadMovesTheBadgeByOne()
    {
        NotificationsAre(Sample.Notification(7));
        CounterSays(new UnreadTally(5, 0, 0));
        using NotificationsViewModel page = CreatePage();
        await page.LoadAsync();

        await page.Notifications[0].ToggleReadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(page.Notifications[0].IsRead, Is.True);
            Assert.That(services.Unread.Tally.Total, Is.EqualTo(4));
        });
        await services.Api.Received(1).SetNotificationReadAsync(
            NotificationKind.Reply, new NotificationId(7), true, Arg.Any<CancellationToken>());
    }

    /// <summary>A mention comes off the mention count, not the reply count.</summary>
    [Test]
    public async Task MarkingAMentionReadMovesTheMentionCount()
    {
        NotificationsAre(Sample.Notification(9, NotificationKind.Mention));
        CounterSays(new UnreadTally(3, 4, 0));
        using NotificationsViewModel page = CreatePage();
        await page.LoadAsync();

        await page.Notifications[0].ToggleReadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(services.Unread.Tally.Replies, Is.EqualTo(3));
            Assert.That(services.Unread.Tally.Mentions, Is.EqualTo(3));
        });
    }

    /// <summary>
    /// The row moves before the server answers, as the vote arrows do. When the server refuses, the
    /// row and the badge both go back rather than claiming something that did not happen.
    /// </summary>
    [Test]
    public async Task ARefusedMarkPutsTheRowAndTheBadgeBack()
    {
        NotificationsAre(Sample.Notification(7));
        CounterSays(new UnreadTally(5, 0, 0));
        services.Api.SetNotificationReadAsync(
                Arg.Any<NotificationKind>(), Arg.Any<NotificationId>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Notification>(_ => throw new LemmyApiException("lemmy.world answered 502."));

        using NotificationsViewModel page = CreatePage();
        await page.LoadAsync();

        await page.Notifications[0].ToggleReadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(page.Notifications[0].IsRead, Is.False);
            Assert.That(page.Notifications[0].ActionError, Is.EqualTo("lemmy.world answered 502."));
            Assert.That(services.Unread.Tally.Total, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task MarkingEverythingReadEmptiesTheBadgeAndTheRows()
    {
        NotificationsAre(Sample.Notification(7), Sample.Notification(8));
        CounterSays(new UnreadTally(40, 2, 0));
        using NotificationsViewModel page = CreatePage();
        await page.LoadAsync();

        await page.MarkAllReadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(page.Notifications.All(row => row.IsRead), Is.True);
            Assert.That(services.Unread.Tally.Any, Is.False, "including the pages that were never on screen");
            Assert.That(page.CanMarkAllRead, Is.False);
        });
    }

    /// <summary>
    /// The rows stay put. Emptying the list under the reader's finger the moment they press it
    /// would take away the very thing they just acted on.
    /// </summary>
    [Test]
    public async Task MarkingEverythingReadLeavesTheRowsOnScreen()
    {
        NotificationsAre(Sample.Notification(7), Sample.Notification(8));
        using NotificationsViewModel page = CreatePage();
        await page.LoadAsync();

        await page.MarkAllReadCommand.ExecuteAsync(null);

        Assert.That(page.Notifications, Has.Count.EqualTo(2));
    }

    /// <summary>Opening one is what seeing it means, so it stops counting on the way to the post.</summary>
    [Test]
    public async Task OpeningANotificationMarksItReadAndGoesToThePost()
    {
        NotificationsAre(Sample.Notification(7, postId: 10));
        CounterSays(new UnreadTally(2, 0, 0));
        services.Api.GetPostAsync(Arg.Any<PostId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Sample.PostSummary()));

        using NotificationsViewModel page = CreatePage();
        await page.LoadAsync();

        await page.Notifications[0].OpenCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(page.Notifications[0].IsRead, Is.True);
            Assert.That(services.Unread.Tally.Total, Is.EqualTo(1));
            Assert.That(navigator.Pushed.OfType<PostDetailViewModel>().Count(), Is.EqualTo(1));
        });
    }

    /// <summary>Failing to record that it was seen must not cost the reader the post.</summary>
    [Test]
    public async Task ANotificationThatCannotBeMarkedReadStillOpens()
    {
        NotificationsAre(Sample.Notification(7));
        services.Api.SetNotificationReadAsync(
                Arg.Any<NotificationKind>(), Arg.Any<NotificationId>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Notification>(_ => throw new LemmyApiException("lemmy.world answered 502."));
        services.Api.GetPostAsync(Arg.Any<PostId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Sample.PostSummary()));

        using NotificationsViewModel page = CreatePage();
        await page.LoadAsync();

        await page.Notifications[0].OpenCommand.ExecuteAsync(null);

        Assert.That(navigator.Pushed.OfType<PostDetailViewModel>().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task TheFilterIsSentToTheServer()
    {
        using NotificationsViewModel page = CreatePage();
        await page.LoadAsync();

        await page.ToggleFilterCommand.ExecuteAsync(null);

        await services.Api.Received(1).GetNotificationsAsync(
            Arg.Is<NotificationQuery>(query => query.UnreadOnly), Arg.Any<CancellationToken>());
        await services.Api.Received(1).GetNotificationsAsync(
            Arg.Is<NotificationQuery>(query => !query.UnreadOnly), Arg.Any<CancellationToken>());
    }
}
