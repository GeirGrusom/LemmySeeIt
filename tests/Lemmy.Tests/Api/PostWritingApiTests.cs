using System.Net;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Tests.TestSupport;

namespace Lemmy.Tests.Api;

/// <summary>Making, rewriting and deleting a post.</summary>
[TestFixture]
internal sealed class PostWritingApiTests
{
    private const string PostResponseBody = """
        {"post_view":{"my_vote":0,
         "post":{"id":7,"name":"A headline","body":"Something to say.","community_id":2,"creator_id":1,
                 "ap_id":"https://lemmy.world/post/7","language_id":37,"nsfw":false,"locked":false,
                 "removed":false,"deleted":false,"published":"2026-08-22T10:00:00","updated":null},
         "creator":{"id":1,"name":"someone","actor_id":"https://lemmy.world/u/someone","published":"2020-01-01T00:00:00"},
         "community":{"id":2,"name":"tech","title":"Tech","actor_id":"https://lemmy.world/c/tech","published":"2020-01-01T00:00:00"},
         "counts":{"score":1,"upvotes":1,"downvotes":0,"comments":0}}}
        """;

    private static LemmyApiClient SignedIn(StubHttpMessageHandler handler) =>
        new(handler.CreateClient(), Sample.Instance, new SessionToken("jwt.token.value"));

    private static PostDraft Draft(string title = "A headline", string url = "", string body = "", bool isNsfw = false)
    {
        _ = PostDraft.TryCreate(title.AsSpan(), url.AsSpan(), body.AsSpan(), isNsfw, out PostDraft draft);
        return draft;
    }

    [Test]
    public async Task PostingSendsTheTitleAndTheCommunity()
    {
        using var handler = StubHttpMessageHandler.Returning(PostResponseBody);

        PostSummary posted = await SignedIn(handler).CreatePostAsync(new CommunityId(2), Draft());

        Assert.Multiple(() =>
        {
            Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/post"));
            Assert.That(handler.SentMethod, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.SentBody, Does.Contain("\"name\":\"A headline\"").And.Contain("\"community_id\":2"));
            Assert.That(handler.SentAuthorization, Is.EqualTo("Bearer jwt.token.value"));
            Assert.That(posted.Post.Id, Is.EqualTo(new PostId(7)));
            Assert.That(posted.Community.Id, Is.EqualTo(new CommunityId(2)));
        });
    }

    [Test]
    public async Task APostWithNoLinkOrBodySendsNeither()
    {
        using var handler = StubHttpMessageHandler.Returning(PostResponseBody);

        await SignedIn(handler).CreatePostAsync(new CommunityId(2), Draft());

        // An empty URL is not the same as no URL: Lemmy would reject "" as a malformed address.
        Assert.That(handler.SentBody, Does.Not.Contain("url").And.Not.Contain("body"));
    }

    [Test]
    public async Task ALinkPostCarriesItsLinkAndFlags()
    {
        using var handler = StubHttpMessageHandler.Returning(PostResponseBody);

        await SignedIn(handler).CreatePostAsync(
            new CommunityId(2),
            Draft(url: "example.com/article", body: "Worth reading.", isNsfw: true));

        Assert.That(
            handler.SentBody,
            Does.Contain("\"url\":\"https://example.com/article\"")
                .And.Contain("\"body\":\"Worth reading.\"")
                .And.Contain("\"nsfw\":true"));
    }

    [Test]
    public async Task EditingUsesPutAndCarriesTheNewText()
    {
        using var handler = StubHttpMessageHandler.Returning(PostResponseBody);

        PostSummary edited = await SignedIn(handler).EditPostAsync(new PostId(7), Draft("Rewritten", body: "New text."));

        Assert.Multiple(() =>
        {
            Assert.That(handler.SentMethod, Is.EqualTo(HttpMethod.Put));
            Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/post"));
            Assert.That(handler.SentBody, Does.Contain("\"post_id\":7").And.Contain("\"name\":\"Rewritten\""));
            Assert.That(edited.Post.Title.Value, Is.EqualTo("A headline"), "the server's copy wins over what was sent");
        });
    }

    [Test]
    public async Task AnEditSendsEmptyFieldsSoALinkCanBeTakenAway()
    {
        using var handler = StubHttpMessageHandler.Returning(PostResponseBody);

        await SignedIn(handler).EditPostAsync(new PostId(7), Draft("Rewritten"));

        // Lemmy reads a missing field as "leave it alone" and an empty one as "clear it", so a post
        // that lost its link has to say so explicitly.
        Assert.That(handler.SentBody, Does.Contain("\"url\":\"\"").And.Contain("\"body\":\"\""));
    }

    [Test]
    public async Task DeletingFlagsThePostRatherThanRemovingIt()
    {
        using var handler = StubHttpMessageHandler.Returning(PostResponseBody);

        await SignedIn(handler).SetPostDeletedAsync(new PostId(7), deleted: true);

        Assert.Multiple(() =>
        {
            Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/post/delete"));
            Assert.That(handler.SentMethod, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.SentBody, Does.Contain("\"post_id\":7").And.Contain("\"deleted\":true"));
        });
    }

    [Test]
    public async Task RestoringIsTheSameCallSaidTheOtherWay()
    {
        using var handler = StubHttpMessageHandler.Returning(PostResponseBody);

        await SignedIn(handler).SetPostDeletedAsync(new PostId(7), deleted: false);

        Assert.That(handler.SentBody, Does.Contain("\"deleted\":false"));
    }

    [Test]
    public void SignedOutNothingIsEvenAttempted()
    {
        using var handler = StubHttpMessageHandler.Returning(PostResponseBody);
        var anonymous = new LemmyApiClient(handler.CreateClient(), Sample.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(
                async () => await anonymous.CreatePostAsync(new CommunityId(2), Draft()),
                Throws.TypeOf<LemmyApiException>().With.Message.Contains("signed in"));
            Assert.That(handler.RequestedUris, Is.Empty);
        });
    }

    [Test]
    public void ADraftWithNoTitleNeverReachesTheServer()
    {
        using var handler = StubHttpMessageHandler.Returning(PostResponseBody);

        Assert.Multiple(() =>
        {
            Assert.That(
                async () => await SignedIn(handler).CreatePostAsync(new CommunityId(2), default),
                Throws.TypeOf<LemmyApiException>());
            Assert.That(handler.RequestedUris, Is.Empty);
        });
    }

    [Test]
    public void ARefusedPostSurfacesAsAnApiFailure()
    {
        using var handler = StubHttpMessageHandler.Returning(
            """{"error":"community_ban"}""", HttpStatusCode.BadRequest);

        Assert.That(
            async () => await SignedIn(handler).CreatePostAsync(new CommunityId(2), Draft()),
            Throws.TypeOf<LemmyApiException>());
    }

    [Test]
    public void AnAcceptedPostThatComesBackEmptyIsStillAFailure()
    {
        using var handler = StubHttpMessageHandler.Returning("{}");

        Assert.That(
            async () => await SignedIn(handler).CreatePostAsync(new CommunityId(2), Draft()),
            Throws.TypeOf<LemmyApiException>().With.Message.Contains("did not send the post back"));
    }
}
