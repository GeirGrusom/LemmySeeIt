using System.Collections.Immutable;
using System.Text.Json;
using Lemmy.Api;
using Lemmy.Api.Dto;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Tests.TestSupport;

namespace Lemmy.Tests.Api;

[TestFixture]
internal sealed class CommentTreeBuilderTests
{
    private static ImmutableArray<CommentViewWire> Parse(string json) =>
        JsonSerializer.Deserialize(json, LemmyJson.Context.GetCommentsResponse)!.Comments;

    [Test]
    public void AnEmptyResponseBuildsAnEmptyThread() =>
        Assert.That(CommentTreeBuilder.Build([]), Is.SameAs(CommentThread.Empty));

    /// <summary>
    /// The wire order is the sort order, not the tree order: a reply can arrive before its parent.
    /// The tree has to come from the paths.
    /// </summary>
    [Test]
    public void RepliesAttachToTheirParentWhicheverOrderTheyArriveIn()
    {
        CommentThread thread = CommentTreeBuilder.Build(Parse(WireFixtures.CommentList));

        Assert.Multiple(() =>
        {
            Assert.That(thread.Roots.Select(node => node.Id), Is.EqualTo(new[] { new CommentId(100), new CommentId(300) }));
            Assert.That(thread.Roots[0].Replies.Single().Id, Is.EqualTo(new CommentId(200)));
            Assert.That(thread.Roots[1].Replies, Is.Empty);
        });
    }

    [Test]
    public void SiblingOrderFollowsTheServerNotThePath()
    {
        CommentThread thread = CommentTreeBuilder.Build(Parse(WireFixtures.CommentList));

        // 100 came second in the response but is the first root, because 200 is not a root at all.
        Assert.That(thread.Roots[0].Id, Is.EqualTo(new CommentId(100)));
    }

    /// <summary>
    /// "Load more replies" returns a sub-thread whose top comment has a parent we did not fetch.
    /// Treating that as a root is what makes the sub-thread renderable on its own.
    /// </summary>
    [Test]
    public void ACommentWhoseParentIsAbsentBecomesARoot()
    {
        CommentThread thread = CommentTreeBuilder.Build(Parse(WireFixtures.CommentSubThread));

        Assert.Multiple(() =>
        {
            Assert.That(thread.Roots.Single().Id, Is.EqualTo(new CommentId(400)));
            Assert.That(thread.Roots.Single().Depth, Is.EqualTo(3));
        });
    }

    [Test]
    public void FlattenReturnsParentsBeforeTheirReplies()
    {
        CommentThread thread = CommentTreeBuilder.Build(Parse(WireFixtures.CommentList));

        Assert.That(
            thread.Flatten().Select(node => node.Id.Value),
            Is.EqualTo(new[] { 100, 200, 300 }));
    }

    [Test]
    public void TalliesAndBadgesSurviveTheMapping()
    {
        CommentNode root = CommentTreeBuilder.Build(Parse(WireFixtures.CommentList)).Roots[0];

        Assert.Multiple(() =>
        {
            Assert.That(root.Tally.Score, Is.EqualTo(new Score(9)));
            Assert.That(root.CreatorIsModerator, Is.True);
            Assert.That(root.Creator.Name.Value, Is.EqualTo("alice"));
        });
    }

    /// <summary>
    /// The server says three descendants; one arrived. The difference is what a "12 more replies"
    /// affordance is built from.
    /// </summary>
    [Test]
    public void UnloadedReplyCountIsWhatTheServerPromisedMinusWhatArrived()
    {
        CommentNode root = CommentTreeBuilder.Build(Parse(WireFixtures.CommentList)).Roots[0];

        Assert.That(root.UnloadedReplyCount, Is.EqualTo(2));
    }

    [Test]
    public void UnloadedReplyCountNeverGoesNegative()
    {
        CommentNode node = Sample.CommentNode(1, "0.1", childCount: 0, Sample.CommentNode(2, "0.1.2"));

        Assert.That(node.UnloadedReplyCount, Is.Zero);
    }

    [Test]
    public void ACommentWithAnUnreadablePathIsDroppedRatherThanMisplaced()
    {
        const string json = """
        {
          "comments": [
            {
              "comment": {
                "id": 1, "creator_id": 1, "post_id": 10, "content": "x", "path": "nonsense",
                "ap_id": "https://lemmy.world/comment/1", "published": "2026-01-01T00:00:00Z",
                "removed": false, "deleted": false, "distinguished": false, "local": true, "language_id": 0
              },
              "creator": {
                "id": 1, "name": "alice", "actor_id": "https://lemmy.world/u/alice",
                "published": "2023-01-01T00:00:00Z", "local": true, "banned": false,
                "deleted": false, "bot_account": false, "instance_id": 1
              },
              "creator_is_moderator": false, "creator_is_admin": false,
              "counts": { "comment_id": 1, "score": 0, "upvotes": 0, "downvotes": 0, "child_count": 0 }
            }
          ]
        }
        """;

        Assert.That(CommentTreeBuilder.Build(Parse(json)).Roots, Is.Empty);
    }
}
