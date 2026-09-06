using System.Collections.Immutable;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Tests.TestSupport;

namespace Lemmy.Tests.Api;

/// <summary>
/// The two lists Lemmy keeps of what has been addressed to an account, and the counter above them.
/// </summary>
[TestFixture]
internal sealed class NotificationApiTests
{
    private static readonly SessionToken Session = new("jwt-token");

    private static LemmyApiClient SignedIn(StubHttpMessageHandler handler) =>
        new(handler.CreateClient(), Sample.Instance, Session);

    [Test]
    public async Task TheCounterIsRead()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.UnreadCount);

        UnreadTally tally = await SignedIn(handler).GetUnreadCountAsync();

        Assert.Multiple(() =>
        {
            Assert.That(tally.Replies, Is.EqualTo(3));
            Assert.That(tally.Mentions, Is.EqualTo(2));
            Assert.That(tally.PrivateMessages, Is.EqualTo(7));
        });
    }

    /// <summary>
    /// The badge counts what this app can show. Direct messages are counted by the server but there
    /// is nowhere here to read one, so including them would put up a number nothing could clear.
    /// </summary>
    [Test]
    public async Task TheBadgeLeavesOutWhatTheAppCannotShow()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.UnreadCount);

        UnreadTally tally = await SignedIn(handler).GetUnreadCountAsync();

        Assert.Multiple(() =>
        {
            Assert.That(tally.Total, Is.EqualTo(5), "replies and mentions, not the seven messages");
            Assert.That(tally.Label, Is.EqualTo("5"));
        });
    }

    /// <summary>Being signed out is an ordinary answer to "what is waiting", not a failure.</summary>
    [Test]
    public async Task SignedOutMeansNothingIsWaitingRatherThanAnError()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.UnreadCount);
        var anonymous = new LemmyApiClient(handler.CreateClient(), Sample.Instance);

        UnreadTally tally = await anonymous.GetUnreadCountAsync();

        Assert.Multiple(() =>
        {
            Assert.That(tally.Any, Is.False);
            Assert.That(handler.RequestCount, Is.Zero, "and no request was made to find that out");
        });
    }

    [TestCase(0, 0, "0")]
    [TestCase(1, 0, "1")]
    [TestCase(60, 39, "99")]
    [TestCase(99, 1, "99+")]
    public void ABadgeStopsCountingOnceTheNumberStopsMeaningAnything(int replies, int mentions, string expected) =>
        Assert.That(new UnreadTally(replies, mentions, 0).Label, Is.EqualTo(expected));

    /// <summary>A count the server got wrong should not become a negative badge.</summary>
    [Test]
    public void NegativeCountsAreClampedAway() =>
        Assert.That(new UnreadTally(-4, -1, -9).Total, Is.Zero);

    /// <summary>
    /// The two lists are separate requests, and the reader is owed one list in time order rather
    /// than all the replies followed by all the mentions.
    /// </summary>
    [Test]
    public async Task RepliesAndMentionsComeBackAsOneListNewestFirst()
    {
        using var handler = StubHttpMessageHandler.ReturningByPath(
            ("user/replies", WireFixtures.ReplyList), ("user/mention", WireFixtures.MentionList));

        ImmutableArray<Notification> notifications =
            await SignedIn(handler).GetNotificationsAsync(new NotificationQuery());

        Assert.Multiple(() =>
        {
            Assert.That(notifications, Has.Length.EqualTo(2));

            // The mention was raised on 2 September, the reply on the 1st.
            Assert.That(notifications[0].Kind, Is.EqualTo(NotificationKind.Mention));
            Assert.That(notifications[1].Kind, Is.EqualTo(NotificationKind.Reply));
        });
    }

    /// <summary>
    /// The marker's identifier, not the comment's. Marking the wrong row read would silently leave
    /// the badge up and mark somebody else's notification instead.
    /// </summary>
    [Test]
    public async Task ANotificationIsIdentifiedByItsRowNotByItsComment()
    {
        using var handler = StubHttpMessageHandler.ReturningByPath(
            ("user/replies", WireFixtures.ReplyList), ("user/mention", WireFixtures.MentionList));

        ImmutableArray<Notification> notifications =
            await SignedIn(handler).GetNotificationsAsync(new NotificationQuery());

        Notification reply = notifications.Single(n => n.Kind == NotificationKind.Reply);

        Assert.Multiple(() =>
        {
            Assert.That(reply.Id, Is.EqualTo(new NotificationId(7)));
            Assert.That(reply.Comment.Id, Is.EqualTo(new CommentId(200)));
            Assert.That(reply.IsRead, Is.False);
            Assert.That(reply.MyVote, Is.EqualTo(Vote.Up));
        });
    }

    /// <summary>
    /// The timestamp on the marker, not on the comment. A federated comment can reach this instance
    /// long after it was written, and it is the arrival that the reader is being told about.
    /// </summary>
    [Test]
    public async Task ANotificationIsDatedFromWhenItWasRaised()
    {
        using var handler = StubHttpMessageHandler.ReturningByPath(
            ("user/replies", WireFixtures.ReplyList), ("user/mention", WireFixtures.MentionList));

        ImmutableArray<Notification> notifications =
            await SignedIn(handler).GetNotificationsAsync(new NotificationQuery());

        Notification reply = notifications.Single(n => n.Kind == NotificationKind.Reply);

        Assert.Multiple(() =>
        {
            Assert.That(reply.Received, Is.EqualTo(DateTimeOffset.Parse("2026-09-01T10:00:00Z", null)));
            Assert.That(reply.Comment.Published, Is.EqualTo(DateTimeOffset.Parse("2026-08-21T11:00:00Z", null)));
        });
    }

    [Test]
    public async Task TheNotificationListsAskTheRightUrls()
    {
        using var handler = StubHttpMessageHandler.ReturningByPath(
            ("user/replies", WireFixtures.ReplyList), ("user/mention", WireFixtures.MentionList));

        await SignedIn(handler).GetNotificationsAsync(
            new NotificationQuery(CommentSortType.New, 2, PageSize.Clamp(50), UnreadOnly: false));

        Assert.That(
            handler.RequestedUris.Select(uri => uri.PathAndQuery),
            Is.EquivalentTo(new[]
            {
                "/api/v3/user/replies?sort=New&page=2&limit=50&unread_only=false",
                "/api/v3/user/mention?sort=New&page=2&limit=50&unread_only=false",
            }));
    }

    /// <summary>The two kinds are marked read through different endpoints; the kind decides which.</summary>
    [Test]
    public async Task MarkingAReplyReadGoesToTheReplyEndpoint()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.MarkedReplyRead);

        await SignedIn(handler).SetNotificationReadAsync(NotificationKind.Reply, new NotificationId(7), true);

        Assert.Multiple(() =>
        {
            Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/comment/mark_as_read"));
            Assert.That(handler.SentBody, Does.Contain("\"comment_reply_id\":7").And.Contain("\"read\":true"));
        });
    }

    [Test]
    public async Task MarkingAMentionReadGoesToTheMentionEndpoint()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.MarkedMentionRead);

        await SignedIn(handler).SetNotificationReadAsync(NotificationKind.Mention, new NotificationId(9), false);

        Assert.Multiple(() =>
        {
            Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/user/mention/mark_as_read"));
            Assert.That(handler.SentBody, Does.Contain("\"person_mention_id\":9").And.Contain("\"read\":false"));
        });
    }

    [Test]
    public async Task MarkingEverythingReadIsOneRequest()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.ReplyList);

        await SignedIn(handler).MarkEverythingReadAsync();

        Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/user/mark_all_as_read"));
    }

    /// <summary>Marking something read is a write, and writes refuse before the request.</summary>
    [Test]
    public void MarkingReadWithoutASessionRefusesBeforeAsking()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.MarkedReplyRead);
        var anonymous = new LemmyApiClient(handler.CreateClient(), Sample.Instance);

        Assert.ThrowsAsync<LemmyApiException>(
            () => anonymous.SetNotificationReadAsync(NotificationKind.Reply, new NotificationId(7), true));
        Assert.That(handler.RequestCount, Is.Zero);
    }
}
