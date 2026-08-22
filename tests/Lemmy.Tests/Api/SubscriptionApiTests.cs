using System.Net;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Tests.TestSupport;

namespace Lemmy.Tests.Api;

/// <summary>Following and unfollowing a community, and reading the state back out of a listing.</summary>
[TestFixture]
internal sealed class SubscriptionApiTests
{
    private const string FollowResponse = """
        {"community_view":{"subscribed":"Pending",
         "community":{"id":2,"name":"tech","title":"Tech","actor_id":"https://lemmy.world/c/tech","published":"2020-01-01T00:00:00"},
         "counts":{"subscribers":100,"posts":10,"comments":50,"users_active_month":30}}}
        """;

    private const string CommunityList = """
        {"communities":[
         {"subscribed":"Subscribed",
          "community":{"id":2,"name":"tech","title":"Tech","actor_id":"https://lemmy.world/c/tech","published":"2020-01-01T00:00:00"},
          "counts":{"subscribers":100,"posts":10,"comments":50,"users_active_month":30}},
         {"subscribed":"NotSubscribed",
          "community":{"id":3,"name":"news","title":"News","actor_id":"https://lemmy.world/c/news","published":"2020-01-01T00:00:00"},
          "counts":{"subscribers":9,"posts":1,"comments":2,"users_active_month":3}}]}
        """;

    private static LemmyApiClient SignedIn(StubHttpMessageHandler handler) =>
        new(handler.CreateClient(), Sample.Instance, new SessionToken("jwt.token.value"));

    [Test]
    public async Task SubscribingPostsFollowTrueToTheCommunityEndpoint()
    {
        using var handler = StubHttpMessageHandler.Returning(FollowResponse);

        SubscriptionState state = await SignedIn(handler).SetSubscriptionAsync(new CommunityId(2), follow: true);

        Assert.Multiple(() =>
        {
            Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/community/follow"));
            Assert.That(handler.SentBody, Does.Contain("\"community_id\":2").And.Contain("\"follow\":true"));
            Assert.That(handler.SentAuthorization, Is.EqualTo("Bearer jwt.token.value"));

            // A remote community has to acknowledge over federation, so Pending is the normal answer.
            Assert.That(state, Is.EqualTo(SubscriptionState.Pending));
        });
    }

    [Test]
    public async Task UnsubscribingSendsFollowFalse()
    {
        using var handler = StubHttpMessageHandler.Returning(
            FollowResponse.Replace("\"Pending\"", "\"NotSubscribed\"", StringComparison.Ordinal));

        SubscriptionState state = await SignedIn(handler).SetSubscriptionAsync(new CommunityId(2), follow: false);

        Assert.Multiple(() =>
        {
            Assert.That(handler.SentBody, Does.Contain("\"follow\":false"));
            Assert.That(state, Is.EqualTo(SubscriptionState.NotSubscribed));
        });
    }

    [Test]
    public void SubscribingWithoutASessionFailsBeforeAnythingIsSent()
    {
        using var handler = StubHttpMessageHandler.Returning(FollowResponse);
        var anonymous = new LemmyApiClient(handler.CreateClient(), Sample.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(
                async () => await anonymous.SetSubscriptionAsync(new CommunityId(2), follow: true),
                Throws.TypeOf<LemmyApiException>().With.Message.Contains("signed in"));
            Assert.That(handler.RequestedUris, Is.Empty);
        });
    }

    [Test]
    public void ARefusedFollowSurfacesAsAnApiFailure()
    {
        using var handler = StubHttpMessageHandler.Returning(
            """{"error":"banned_from_community"}""", HttpStatusCode.BadRequest);

        Assert.That(
            async () => await SignedIn(handler).SetSubscriptionAsync(new CommunityId(2), follow: true),
            Throws.TypeOf<LemmyApiException>());
    }

    [Test]
    public async Task ADirectoryListingCarriesEachCommunitysSubscriptionState()
    {
        using var handler = StubHttpMessageHandler.Returning(CommunityList);

        var communities = await SignedIn(handler).GetCommunitiesAsync(new CommunityQuery());

        Assert.Multiple(() =>
        {
            Assert.That(communities[0].Subscription, Is.EqualTo(SubscriptionState.Subscribed));
            Assert.That(communities[1].Subscription, Is.EqualTo(SubscriptionState.NotSubscribed));
        });
    }

    [Test]
    public async Task AFeedPostSaysWhetherItsCommunityIsFollowed()
    {
        // The real fixture with the one field added, rather than a hand-rolled post the mapper
        // would reject for missing something unrelated.
        using var handler = StubHttpMessageHandler.Returning(
            WireFixtures.PostList.Replace("\"post\": {", "\"subscribed\": \"Subscribed\", \"post\": {", StringComparison.Ordinal));

        PostPage page = await SignedIn(handler).GetFeedAsync(new FeedQuery());

        Assert.That(page.Posts[0].Subscription, Is.EqualTo(SubscriptionState.Subscribed));
    }
}
