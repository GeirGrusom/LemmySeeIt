using System.Net;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.Session;

[TestFixture]
internal sealed class SignInTests
{
    private const string LoginBody = """{"jwt":"a-real-looking-token","registration_created":false,"verify_email_sent":false}""";

    private const string SiteWithAccount = """
    {
      "site_view": { "site": { "id": 1, "name": "Lemmy.World", "actor_id": "https://lemmy.world/" },
                     "counts": { "users": 1, "posts": 1, "comments": 1, "communities": 1 } },
      "my_user": { "local_user_view": { "person": {
          "id": 42, "name": "alice", "display_name": "Alice",
          "actor_id": "https://lemmy.world/u/alice", "published": "2023-01-01T00:00:00Z",
          "local": true, "banned": false, "deleted": false, "bot_account": false, "instance_id": 1 } } },
      "version": "0.19.19"
    }
    """;

    private static LemmyApiClient Client(StubHttpMessageHandler handler, SessionToken session = default) =>
        new(handler.CreateClient(), Sample.Instance, session);

    [Test]
    public async Task SigningInReturnsTheToken()
    {
        using var handler = StubHttpMessageHandler.Returning(LoginBody);

        SessionToken token = await Client(handler).LogInAsync(new LoginRequest("alice", "hunter2"));

        Assert.Multiple(() =>
        {
            Assert.That(token.Value, Is.EqualTo("a-real-looking-token"));
            Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/user/login"));
        });
    }

    [Test]
    public void RefusedCredentialsSurfaceTheServersReason()
    {
        using var handler = StubHttpMessageHandler.Returning("""{"error":"incorrect_login"}""", HttpStatusCode.Unauthorized);

        LemmyApiException? thrown = Assert.ThrowsAsync<LemmyApiException>(
            async () => await Client(handler).LogInAsync(new LoginRequest("alice", "wrong")));

        Assert.That(thrown!.ServerError, Is.EqualTo("incorrect_login"));
    }

    /// <summary>
    /// A sign-in can succeed and still not give you a session — the instance may be holding the
    /// account for approval. Saying "no token" would leave the reader with nothing to act on.
    /// </summary>
    [Test]
    public void AnAccountAwaitingApprovalSaysSo()
    {
        using var handler = StubHttpMessageHandler.Returning("""{"registration_created":true}""");

        LemmyApiException? thrown = Assert.ThrowsAsync<LemmyApiException>(
            async () => await Client(handler).LogInAsync(new LoginRequest("alice", "hunter2")));

        Assert.That(thrown!.Message, Does.Contain("approve"));
    }

    [Test]
    public void AnAccountAwaitingEmailConfirmationSaysSo()
    {
        using var handler = StubHttpMessageHandler.Returning("""{"verify_email_sent":true}""");

        LemmyApiException? thrown = Assert.ThrowsAsync<LemmyApiException>(
            async () => await Client(handler).LogInAsync(new LoginRequest("alice", "hunter2")));

        Assert.That(thrown!.Message, Does.Contain("email"));
    }

    [Test]
    public void IncompleteCredentialsNeverReachTheServer()
    {
        using var handler = StubHttpMessageHandler.Returning(LoginBody);

        Assert.ThrowsAsync<LemmyApiException>(
            async () => await Client(handler).LogInAsync(new LoginRequest("alice", string.Empty)));

        Assert.That(handler.RequestedUris, Is.Empty);
    }

    [Test]
    public async Task AnAuthenticatedClientSendsItsToken()
    {
        using var handler = StubHttpMessageHandler.Returning(SiteWithAccount);

        await Client(handler, new SessionToken("a-real-looking-token")).GetMyAccountAsync();

        Assert.That(handler.SentAuthorization, Is.EqualTo("Bearer a-real-looking-token"));
    }

    [Test]
    public async Task AnAnonymousClientSendsNoToken()
    {
        using var handler = StubHttpMessageHandler.Returning(SiteWithAccount);

        await Client(handler).GetSiteAsync();

        Assert.That(handler.SentAuthorization, Is.Null);
    }

    [Test]
    public async Task TheAccountComesBackFromTheSiteResponse()
    {
        using var handler = StubHttpMessageHandler.Returning(SiteWithAccount);

        Account? account = await Client(handler, new SessionToken("t")).GetMyAccountAsync();

        Assert.Multiple(() =>
        {
            Assert.That(account?.Name.Value, Is.EqualTo("alice"));
            Assert.That(account?.PreferredName, Is.EqualTo("Alice"));
            Assert.That(account?.QualifiedName, Is.EqualTo("@alice@lemmy.world"));
        });
    }

    /// <summary>
    /// The crucial one. A dead token is not rejected — Lemmy answers 200 and simply leaves the
    /// account out — so the absence of an account is the only signal that a session has expired.
    /// </summary>
    [Test]
    public async Task ADeadTokenLooksLikeSuccessAndIsDetectedByTheMissingAccount()
    {
        const string siteWithoutAccount = """
        { "site_view": { "site": { "id": 1, "name": "Lemmy.World", "actor_id": "https://lemmy.world/" },
                         "counts": { "users": 1, "posts": 1, "comments": 1, "communities": 1 } },
          "version": "0.19.19" }
        """;

        using var handler = StubHttpMessageHandler.Returning(siteWithoutAccount);

        Assert.That(await Client(handler, new SessionToken("stale")).GetMyAccountAsync(), Is.Null);
    }

    [Test]
    public async Task SigningOutTellsTheInstance()
    {
        using var handler = StubHttpMessageHandler.Returning("{}");

        await Client(handler, new SessionToken("t")).LogOutAsync();

        Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/user/logout"));
    }

    [Test]
    public async Task SigningOutWhenNobodyIsSignedInAsksNothing()
    {
        using var handler = StubHttpMessageHandler.Returning("{}");

        await Client(handler).LogOutAsync();

        Assert.That(handler.RequestedUris, Is.Empty);
    }

    /// <summary>A server that is unreachable must not prevent signing out locally.</summary>
    [Test]
    public void SigningOutSurvivesAnUnreachableServer()
    {
        using var handler = StubHttpMessageHandler.Failing(new HttpRequestException("no route"));

        Assert.That(async () => await Client(handler, new SessionToken("t")).LogOutAsync(), Throws.Nothing);
    }

    [Test]
    public async Task VotesComeBackWithThePostsWhenSignedIn()
    {
        const string feed = """
        { "posts": [ {
            "post": { "id": 1, "name": "A post", "creator_id": 1, "community_id": 1,
                      "ap_id": "https://lemmy.world/post/1", "published": "2026-01-01T00:00:00Z",
                      "removed": false, "locked": false, "deleted": false, "nsfw": false,
                      "language_id": 0, "featured_community": false, "featured_local": false },
            "creator": { "id": 1, "name": "alice", "actor_id": "https://lemmy.world/u/alice",
                         "published": "2023-01-01T00:00:00Z", "local": true, "banned": false,
                         "deleted": false, "bot_account": false, "instance_id": 1 },
            "community": { "id": 1, "name": "test", "title": "Test",
                           "actor_id": "https://lemmy.world/c/test", "published": "2023-01-01T00:00:00Z",
                           "local": true, "removed": false, "deleted": false, "nsfw": false,
                           "hidden": false, "posting_restricted_to_mods": false, "instance_id": 1 },
            "creator_is_moderator": false, "creator_is_admin": false, "my_vote": 1,
            "counts": { "post_id": 1, "comments": 0, "score": 5, "upvotes": 5, "downvotes": 0 } } ] }
        """;

        using var handler = StubHttpMessageHandler.Returning(feed);

        PostPage page = await Client(handler, new SessionToken("t")).GetFeedAsync(new FeedQuery());

        Assert.That(page.Posts[0].MyVote, Is.EqualTo(Vote.Up));
    }

    [Test]
    public async Task SignedOutThereAreNoVotesToShow()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.PostList);

        PostPage page = await Client(handler).GetFeedAsync(new FeedQuery());

        Assert.That(page.Posts[0].MyVote, Is.EqualTo(Vote.None));
    }
}
