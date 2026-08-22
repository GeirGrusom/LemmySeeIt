using System.Net;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Tests.TestSupport;

namespace Lemmy.Tests.Api;

/// <summary>
/// Voting is the first thing this client writes rather than reads, so these cover the shape of what
/// goes out as much as what comes back.
/// </summary>
[TestFixture]
internal sealed class VotingApiTests
{
    private const string PostLikeResponse = """
        {"post_view":{"post":{"id":7,"name":"A post","published":"2026-08-01T10:00:00"},
         "creator":{"id":1,"name":"someone","actor_id":"https://lemmy.world/u/someone","published":"2020-01-01T00:00:00"},
         "community":{"id":2,"name":"tech","title":"Tech","actor_id":"https://lemmy.world/c/tech","published":"2020-01-01T00:00:00"},
         "counts":{"score":43,"upvotes":50,"downvotes":7,"comments":3},"my_vote":1}}
        """;

    private const string CommentLikeResponse = """
        {"comment_view":{"comment":{"id":11,"content":"Hi","path":"0.11","published":"2026-08-01T10:00:00"},
         "creator":{"id":1,"name":"someone","actor_id":"https://lemmy.world/u/someone","published":"2020-01-01T00:00:00"},
         "counts":{"score":-2,"upvotes":3,"downvotes":5,"child_count":0},"my_vote":-1}}
        """;

    private static LemmyApiClient SignedIn(StubHttpMessageHandler handler) =>
        new(handler.CreateClient(), Sample.Instance, new SessionToken("jwt.token.value"));

    [Test]
    public async Task VoteOnPostAsync_PostsTheScoreToTheLikeEndpoint()
    {
        using var handler = StubHttpMessageHandler.Returning(PostLikeResponse);

        await SignedIn(handler).VoteOnPostAsync(new PostId(7), Vote.Up);

        Assert.Multiple(() =>
        {
            Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/post/like"));
            Assert.That(handler.SentMethod, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.SentBody, Does.Contain("\"post_id\":7").And.Contain("\"score\":1"));
            Assert.That(handler.SentAuthorization, Is.EqualTo("Bearer jwt.token.value"));
        });
    }

    [Test]
    public async Task VoteOnPostAsync_ReadsTheCountsBackFromTheServer()
    {
        using var handler = StubHttpMessageHandler.Returning(PostLikeResponse);

        VoteOutcome outcome = await SignedIn(handler).VoteOnPostAsync(new PostId(7), Vote.Up);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.MyVote, Is.EqualTo(Vote.Up));
            Assert.That(outcome.Score.Value, Is.EqualTo(43));
            Assert.That(outcome.Upvotes.Value, Is.EqualTo(50));
            Assert.That(outcome.Downvotes.Value, Is.EqualTo(7));
        });
    }

    [Test]
    public async Task VoteOnCommentAsync_PostsTheScoreToTheLikeEndpoint()
    {
        using var handler = StubHttpMessageHandler.Returning(CommentLikeResponse);

        VoteOutcome outcome = await SignedIn(handler).VoteOnCommentAsync(new CommentId(11), Vote.Down);

        Assert.Multiple(() =>
        {
            Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/comment/like"));
            Assert.That(handler.SentBody, Does.Contain("\"comment_id\":11").And.Contain("\"score\":-1"));
            Assert.That(outcome.MyVote, Is.EqualTo(Vote.Down));
            Assert.That(outcome.Score.Value, Is.EqualTo(-2));
        });
    }

    [Test]
    public async Task TakingAVoteBackSendsZeroRatherThanOmittingTheField()
    {
        using var handler = StubHttpMessageHandler.Returning(PostLikeResponse);

        await SignedIn(handler).VoteOnPostAsync(new PostId(7), Vote.None);

        // WhenWritingNull would drop a null, but zero is a real instruction: it retracts the vote.
        Assert.That(handler.SentBody, Does.Contain("\"score\":0"));
    }

    [Test]
    public void VotingWithoutASessionFailsBeforeAnythingIsSent()
    {
        using var handler = StubHttpMessageHandler.Returning(PostLikeResponse);
        var anonymous = new LemmyApiClient(handler.CreateClient(), Sample.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(
                async () => await anonymous.VoteOnPostAsync(new PostId(7), Vote.Up),
                Throws.TypeOf<LemmyApiException>().With.Message.Contains("signed in"));
            Assert.That(handler.RequestedUris, Is.Empty);
        });
    }

    [Test]
    public void AVoteTheInstanceRefusesSurfacesAsAnApiFailure()
    {
        using var handler = StubHttpMessageHandler.Returning(
            """{"error":"couldnt_like_post"}""", HttpStatusCode.BadRequest);

        Assert.That(
            async () => await SignedIn(handler).VoteOnPostAsync(new PostId(7), Vote.Up),
            Throws.TypeOf<LemmyApiException>());
    }
}
