using System.Net;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Tests.TestSupport;

namespace Lemmy.Tests.Api;

/// <summary>Posting, editing and deleting a comment.</summary>
[TestFixture]
internal sealed class CommentWritingApiTests
{
    private const string CommentResponseBody = """
        {"comment_view":{"my_vote":0,
         "comment":{"id":11,"post_id":10,"creator_id":1,"content":"Said something",
                    "path":"0.11","ap_id":"https://lemmy.world/comment/11","language_id":37,
                    "removed":false,"deleted":false,"distinguished":false,
                    "published":"2026-08-22T10:00:00","updated":null},
         "creator":{"id":1,"name":"someone","actor_id":"https://lemmy.world/u/someone","published":"2020-01-01T00:00:00"},
         "counts":{"score":1,"upvotes":1,"downvotes":0,"child_count":0}}}
        """;

    private static LemmyApiClient SignedIn(StubHttpMessageHandler handler) =>
        new(handler.CreateClient(), Sample.Instance, new SessionToken("jwt.token.value"));

    private static CommentDraft Draft(string text = "Said something")
    {
        _ = CommentDraft.TryCreate(text.AsSpan(), out CommentDraft draft);
        return draft;
    }

    [Test]
    public async Task CommentingOnAPostSendsNoParent()
    {
        using var handler = StubHttpMessageHandler.Returning(CommentResponseBody);

        CommentNode node = await SignedIn(handler).CreateCommentAsync(new PostId(10), null, Draft());

        Assert.Multiple(() =>
        {
            Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/comment"));
            Assert.That(handler.SentMethod, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.SentBody, Does.Contain("\"post_id\":10").And.Contain("\"content\":\"Said something\""));
            Assert.That(handler.SentBody, Does.Not.Contain("parent_id"), "a top-level comment has no parent");
            Assert.That(node.Comment.Id, Is.EqualTo(new CommentId(11)));
            Assert.That(node.Replies, Is.Empty);
        });
    }

    [Test]
    public async Task ReplyingSendsTheParentComment()
    {
        using var handler = StubHttpMessageHandler.Returning(CommentResponseBody);

        await SignedIn(handler).CreateCommentAsync(new PostId(10), new CommentId(7), Draft());

        Assert.That(handler.SentBody, Does.Contain("\"parent_id\":7"));
    }

    [Test]
    public async Task EditingUsesPutAndCarriesTheNewText()
    {
        using var handler = StubHttpMessageHandler.Returning(CommentResponseBody);

        Comment edited = await SignedIn(handler).EditCommentAsync(new CommentId(11), Draft("Rewritten"));

        Assert.Multiple(() =>
        {
            Assert.That(handler.SentMethod, Is.EqualTo(HttpMethod.Put));
            Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/comment"));
            Assert.That(handler.SentBody, Does.Contain("\"comment_id\":11").And.Contain("\"content\":\"Rewritten\""));
            Assert.That(edited.Id, Is.EqualTo(new CommentId(11)));
        });
    }

    [Test]
    public async Task DeletingSendsTheFlagRatherThanRemovingAnything()
    {
        using var handler = StubHttpMessageHandler.Returning(
            CommentResponseBody.Replace("\"deleted\":false", "\"deleted\":true", StringComparison.Ordinal));

        Comment deleted = await SignedIn(handler).SetCommentDeletedAsync(new CommentId(11), deleted: true);

        Assert.Multiple(() =>
        {
            Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/comment/delete"));
            Assert.That(handler.SentBody, Does.Contain("\"comment_id\":11").And.Contain("\"deleted\":true"));
            Assert.That(deleted.IsDeleted, Is.True);
            Assert.That(deleted.VisibleContent.Value, Does.Contain("Deleted by author"));
        });
    }

    [Test]
    public async Task RestoringSendsTheFlagBack()
    {
        using var handler = StubHttpMessageHandler.Returning(CommentResponseBody);

        Comment restored = await SignedIn(handler).SetCommentDeletedAsync(new CommentId(11), deleted: false);

        Assert.Multiple(() =>
        {
            Assert.That(handler.SentBody, Does.Contain("\"deleted\":false"));
            Assert.That(restored.IsDeleted, Is.False);
        });
    }

    [Test]
    public void WritingWithoutASessionFailsBeforeAnythingIsSent()
    {
        using var handler = StubHttpMessageHandler.Returning(CommentResponseBody);
        var anonymous = new LemmyApiClient(handler.CreateClient(), Sample.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(
                async () => await anonymous.CreateCommentAsync(new PostId(10), null, Draft()),
                Throws.TypeOf<LemmyApiException>().With.Message.Contains("signed in"));
            Assert.That(handler.RequestedUris, Is.Empty);
        });
    }

    [Test]
    public void AnEmptyDraftNeverReachesTheServer()
    {
        using var handler = StubHttpMessageHandler.Returning(CommentResponseBody);

        Assert.Multiple(() =>
        {
            Assert.That(
                async () => await SignedIn(handler).CreateCommentAsync(new PostId(10), null, default),
                Throws.TypeOf<LemmyApiException>());
            Assert.That(handler.RequestedUris, Is.Empty);
        });
    }

    [Test]
    public void ARefusedCommentSurfacesAsAnApiFailure()
    {
        using var handler = StubHttpMessageHandler.Returning(
            """{"error":"locked"}""", HttpStatusCode.BadRequest);

        Assert.That(
            async () => await SignedIn(handler).CreateCommentAsync(new PostId(10), null, Draft()),
            Throws.TypeOf<LemmyApiException>());
    }
}
