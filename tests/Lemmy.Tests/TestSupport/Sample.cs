using System.Collections.Immutable;
using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Tests.TestSupport;

/// <summary>
/// Builders for domain models. Every argument has a default, so a test names only what it is
/// actually about and the rest stays out of the way.
/// </summary>
internal static class Sample
{
    internal static InstanceAddress Instance { get; } = InstanceAddress.Parse("lemmy.world");

    internal static Person Person(int id = 1, string name = "alice", bool isBot = false) =>
        new(
            new PersonId(id),
            new Username(name),
            null,
            new ActorId($"https://lemmy.world/u/{name}"),
            null,
            IsLocal: true,
            isBot,
            IsBanned: false,
            IsDeleted: false,
            FixedTimeProvider.Reference.AddYears(-2));

    internal static Community Community(int id = 2, string name = "technology") =>
        new(
            new CommunityId(id),
            new CommunityName(name),
            "Technology",
            MarkdownText.Empty,
            new ActorId($"https://lemmy.world/c/{name}"),
            null,
            null,
            IsLocal: true,
            IsNsfw: false,
            IsRemoved: false,
            IsDeleted: false,
            FixedTimeProvider.Reference.AddYears(-3));

    internal static Post Post(
        int id = 10,
        string title = "A post",
        bool isNsfw = false,
        string? url = null,
        string? contentType = null) =>
        new(
            new PostId(id),
            new PostTitle(title),
            MarkdownText.Empty,
            url is null ? null : WebLink.Parse(url),
            null,
            contentType is null ? null : new MediaType(contentType),
            null,
            null,
            new PersonId(1),
            new CommunityId(2),
            new ActorId($"https://lemmy.world/post/{id}"),
            LanguageId.Undetermined,
            isNsfw,
            IsLocked: false,
            IsRemoved: false,
            IsDeleted: false,
            IsFeaturedInCommunity: false,
            IsFeaturedLocally: false,
            FixedTimeProvider.Reference.AddHours(-3),
            null);

    internal static PostSummary PostSummary(int id = 10, string title = "A post", bool isNsfw = false) =>
        new(Post(id, title, isNsfw), Person(), Community(), new PostTally(new Score(12), new VoteCount(14), new VoteCount(2), new VoteCount(5), null), false, false);

    /// <summary>
    /// A page of posts whose titles vary in length, so the rows differ in height the way real ones
    /// do. Uniform rows would let a virtualising panel estimate its extent exactly, which hides
    /// every bug that only appears once the estimate is approximate.
    /// </summary>
    internal static PostPage PostPage(int count = 3, string? nextCursor = "P1")
    {
        var posts = ImmutableArray.CreateBuilder<PostSummary>(count);
        for (int index = 0; index < count; index++)
        {
            string title = (index % 4) switch
            {
                0 => $"Post {index}",
                1 => $"Post {index} with a rather longer headline that will wrap onto a second line",
                2 => $"Post {index} with a headline long enough to wrap across three separate lines of the card, which is not unusual on a link aggregator",
                _ => $"Post {index} — medium length headline",
            };

            posts.Add(PostSummary(10 + index, title));
        }

        PageCursor? cursor = nextCursor is null ? null : new PageCursor(nextCursor);
        return new PostPage(posts.ToImmutable(), cursor);
    }

    internal static Comment Comment(int id = 100, string path = "0.100", string content = "A comment") =>
        new(
            new CommentId(id),
            new PostId(10),
            new PersonId(1),
            new MarkdownText(content),
            new CommentPath(path),
            new ActorId($"https://lemmy.world/comment/{id}"),
            LanguageId.Undetermined,
            IsRemoved: false,
            IsDeleted: false,
            IsDistinguished: false,
            FixedTimeProvider.Reference.AddHours(-1),
            null);

    internal static CommentNode CommentNode(
        int id = 100,
        string path = "0.100",
        int childCount = 0,
        params CommentNode[] replies) =>
        new(
            Comment(id, path),
            Person(),
            new CommentTally(new Score(3), new VoteCount(3), new VoteCount(0), new VoteCount(childCount)),
            false,
            false,
            [.. replies]);

    /// <summary>A post whose link really is a picture, which is what the image viewer works on.</summary>
    internal static PostSummary ImagePostSummary(int id = 10, string title = "A picture") =>
        PostSummary(id, title) with
        {
            Post = Post(id, title, url: $"https://example.com/art-{id}.png", contentType: "image/png"),
        };

    /// <summary>
    /// A page mixing pictures and articles, as any real feed does. Carries a cursor by default, so
    /// a feed built from one behaves like a feed with more to come.
    /// </summary>
    internal static PostPage MixedPage(int imageCount, int articleCount = 1, string? nextCursor = "P1")
    {
        var posts = ImmutableArray.CreateBuilder<PostSummary>(imageCount + articleCount);
        for (int index = 0; index < imageCount; index++)
        {
            posts.Add(ImagePostSummary(10 + index, $"Picture {index}"));
            if (index < articleCount)
            {
                posts.Add(PostSummary(100 + index, $"Article {index}"));
            }
        }

        PageCursor? cursor = nextCursor is null ? null : new PageCursor(nextCursor);
        return new PostPage(posts.ToImmutable(), cursor);
    }

    internal static CommunitySummary CommunitySummary(int id = 2, string name = "technology") =>
        new(Community(id, name), new CommunityTally(new VoteCount(87409), new VoteCount(21286), new VoteCount(932665), new VoteCount(15822)));

    /// <summary>An account's page, with one post and one comment on it.</summary>
    internal static PersonProfile PersonProfile(int id = 1, string name = "alice") =>
        new(
            Person(id, name),
            new MarkdownText("Reads more than posts."),
            null,
            new PersonTally(new VoteCount(3), new VoteCount(41)),
            IsAdmin: false,
            [PostSummary()],
            [CommentNode()]);

    internal static Notification Notification(
        int id = 7,
        NotificationKind kind = NotificationKind.Reply,
        bool isRead = false,
        int postId = 10,
        string content = "A comment",
        int minutesAgo = 5) =>
        new(
            kind,
            new NotificationId(id),
            Comment(200 + id, $"0.{200 + id}", content),
            Person(2, "bob"),
            Post(postId),
            Community(),
            new CommentTally(new Score(3), new VoteCount(3), new VoteCount(0), new VoteCount(0)),
            FixedTimeProvider.Reference.AddMinutes(-minutesAgo),
            isRead);
}
