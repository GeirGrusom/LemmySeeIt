using Lemmy.Domain;
using Lemmy.Services;

namespace Lemmy.Tests.Services;

/// <summary>
/// The tracker exists so that one community shown in several places cannot disagree with itself.
/// </summary>
[TestFixture]
internal sealed class SubscriptionTrackerTests
{
    private static readonly CommunityId Technology = new(2);
    private static readonly CommunityId News = new(3);

    [Test]
    public void WithNothingRecordedTheFetchedStateIsWhatShows()
    {
        var tracker = new SubscriptionTracker();

        Assert.That(
            tracker.StateFor(Technology, SubscriptionState.Subscribed),
            Is.EqualTo(SubscriptionState.Subscribed));
    }

    [Test]
    public void ARecordedStateOverridesWhateverTheResponseSaid()
    {
        var tracker = new SubscriptionTracker();
        tracker.Record(Technology, SubscriptionState.Subscribed);

        Assert.That(
            tracker.StateFor(Technology, SubscriptionState.NotSubscribed),
            Is.EqualTo(SubscriptionState.Subscribed));
    }

    [Test]
    public void ChangingOneCommunityAnnouncesOnlyThatOne()
    {
        var tracker = new SubscriptionTracker();
        var announced = new List<CommunityId>();
        tracker.Changed += (_, community) => announced.Add(community);

        tracker.Record(Technology, SubscriptionState.Subscribed);

        Assert.That(announced, Is.EqualTo(new[] { Technology }));
    }

    [Test]
    public void RecordingTheStateItAlreadyHasAnnouncesNothing()
    {
        var tracker = new SubscriptionTracker();
        tracker.Record(Technology, SubscriptionState.Subscribed);

        int announcements = 0;
        tracker.Changed += (_, _) => announcements++;
        tracker.Record(Technology, SubscriptionState.Subscribed);

        Assert.That(announcements, Is.Zero);
    }

    [Test]
    public void ClearingForgetsEverythingAndSaysSo()
    {
        var tracker = new SubscriptionTracker();
        tracker.Record(Technology, SubscriptionState.Subscribed);
        tracker.Record(News, SubscriptionState.Pending);

        var announced = new List<CommunityId>();
        tracker.Changed += (_, community) => announced.Add(community);
        tracker.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(announced, Is.EquivalentTo(new[] { Technology, News }));
            Assert.That(
                tracker.StateFor(Technology, SubscriptionState.NotSubscribed),
                Is.EqualTo(SubscriptionState.NotSubscribed),
                "a cleared tracker must fall back to what the response said");
        });
    }

    [Test]
    public void ClearingAnEmptyTrackerAnnouncesNothing()
    {
        var tracker = new SubscriptionTracker();
        int announcements = 0;
        tracker.Changed += (_, _) => announcements++;

        tracker.Clear();

        Assert.That(announcements, Is.Zero);
    }

    [TestCase("Subscribed", SubscriptionState.Subscribed)]
    [TestCase("Pending", SubscriptionState.Pending)]
    [TestCase("NotSubscribed", SubscriptionState.NotSubscribed)]
    [TestCase(null, SubscriptionState.NotSubscribed)]
    [TestCase("something new", SubscriptionState.NotSubscribed)]
    public void TheWireValueIsRead(string? wire, SubscriptionState expected) =>
        Assert.That(wire.ToSubscriptionState(), Is.EqualTo(expected));

    [TestCase(SubscriptionState.Subscribed, true)]
    [TestCase(SubscriptionState.Pending, true)]
    [TestCase(SubscriptionState.NotSubscribed, false)]
    public void APendingFollowCountsAsFollowing(SubscriptionState state, bool expected) =>
        Assert.That(state.IsFollowing(), Is.EqualTo(expected));
}
