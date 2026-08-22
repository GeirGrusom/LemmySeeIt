using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Services;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// Subscribing shows before the server agrees, puts itself back when it does not, and — the point
/// of routing it through the tracker — keeps every place showing that community in agreement.
/// </summary>
[TestFixture]
internal sealed class SubscribeButtonViewModelTests
{
    private static readonly CommunityId Technology = new(2);

    private static SubscribeButtonViewModel Button(
        SubscriptionTracker tracker,
        Func<bool, CancellationToken, Task<SubscriptionState>> send,
        SubscriptionState asFetched = SubscriptionState.NotSubscribed,
        bool canSubscribe = true) =>
        new(Technology, asFetched, canSubscribe, tracker, send);

    [Test]
    public async Task SubscribingShowsAsPendingBeforeTheServerAnswers()
    {
        var gate = new TaskCompletionSource<SubscriptionState>();
        var tracker = new SubscriptionTracker();
        using SubscribeButtonViewModel button = Button(tracker, (_, _) => gate.Task);

        Task toggling = button.ToggleCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(button.IsFollowing, Is.True);
            Assert.That(button.Label, Is.EqualTo("Pending"));
            Assert.That(button.IsBusy, Is.True);
        });

        gate.SetResult(SubscriptionState.Subscribed);
        await toggling;

        Assert.That(button.Label, Is.EqualTo("Subscribed"));
    }

    [Test]
    public async Task AFollowTheCommunityHasNotAcknowledgedStaysPending()
    {
        var tracker = new SubscriptionTracker();
        using SubscribeButtonViewModel button = Button(tracker, (_, _) => Task.FromResult(SubscriptionState.Pending));

        await button.ToggleCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(button.IsPending, Is.True);
            Assert.That(button.IsFollowing, Is.True, "a pending follow still means the button unsubscribes");
        });
    }

    [Test]
    public async Task PressingItWhileFollowingUnsubscribes()
    {
        bool? sent = null;
        var tracker = new SubscriptionTracker();
        using SubscribeButtonViewModel button = Button(
            tracker,
            (follow, _) =>
            {
                sent = follow;
                return Task.FromResult(SubscriptionState.NotSubscribed);
            },
            asFetched: SubscriptionState.Subscribed);

        await button.ToggleCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(sent, Is.False);
            Assert.That(button.IsFollowing, Is.False);
            Assert.That(button.Label, Is.EqualTo("Subscribe"));
        });
    }

    [Test]
    public async Task PressingItWhilePendingAlsoUnsubscribes()
    {
        bool? sent = null;
        var tracker = new SubscriptionTracker();
        using SubscribeButtonViewModel button = Button(
            tracker,
            (follow, _) =>
            {
                sent = follow;
                return Task.FromResult(SubscriptionState.NotSubscribed);
            },
            asFetched: SubscriptionState.Pending);

        await button.ToggleCommand.ExecuteAsync(null);

        Assert.That(sent, Is.False);
    }

    [Test]
    public async Task AFailedSubscribeIsPutBackWithAReason()
    {
        var tracker = new SubscriptionTracker();
        using SubscribeButtonViewModel button = Button(
            tracker,
            (_, _) => throw new LemmyApiException("You are banned from that community."));

        await button.ToggleCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(button.IsFollowing, Is.False);
            Assert.That(button.Label, Is.EqualTo("Subscribe"));
            Assert.That(button.ErrorMessage, Is.EqualTo("You are banned from that community."));
        });
    }

    [Test]
    public async Task TwoPlacesShowingTheSameCommunityStayInAgreement()
    {
        var tracker = new SubscriptionTracker();
        using SubscribeButtonViewModel directoryRow = Button(tracker, (_, _) => Task.FromResult(SubscriptionState.Subscribed));
        using SubscribeButtonViewModel communityHeader = Button(tracker, (_, _) => Task.FromResult(SubscriptionState.Subscribed));

        await directoryRow.ToggleCommand.ExecuteAsync(null);

        Assert.That(communityHeader.IsFollowing, Is.True, "the other control should have followed along");
        Assert.That(communityHeader.Label, Is.EqualTo("Subscribed"));
    }

    [Test]
    public async Task ADisposedControlStopsListening()
    {
        var tracker = new SubscriptionTracker();
        SubscribeButtonViewModel button = Button(tracker, (_, _) => Task.FromResult(SubscriptionState.Subscribed));

        int notifications = 0;
        button.PropertyChanged += (_, _) => notifications++;
        button.Dispose();

        tracker.Record(Technology, SubscriptionState.Subscribed);
        await Task.CompletedTask;

        Assert.That(notifications, Is.Zero);
    }

    [Test]
    public void SignedOutThereIsNothingToPress()
    {
        var tracker = new SubscriptionTracker();
        using SubscribeButtonViewModel button = Button(
            tracker,
            (_, _) => Task.FromResult(SubscriptionState.Subscribed),
            canSubscribe: false);

        Assert.Multiple(() =>
        {
            Assert.That(button.CanSubscribe, Is.False);
            Assert.That(button.ToggleCommand.CanExecute(null), Is.False);
        });
    }
}
