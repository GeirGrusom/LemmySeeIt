namespace Lemmy.Tests.TestSupport;

/// <summary>
/// Response bodies copied from a real Lemmy 0.19 instance and trimmed to the fields this client
/// reads. Kept verbatim rather than generated so that a change in what we ask of the wire format
/// shows up here as an edit.
/// </summary>
internal static class WireFixtures
{
    internal const string PostList = """
    {
      "posts": [
        {
          "post": {
            "id": 50908658,
            "name": "This HAS to be satire",
            "url": "https://lemmy.world/pictrs/image/4f25b9b2.png",
            "body": "",
            "creator_id": 306405,
            "community_id": 79185,
            "removed": false,
            "locked": false,
            "published": "2026-08-20T20:31:39.106789Z",
            "deleted": false,
            "nsfw": false,
            "thumbnail_url": "https://lemmy.world/pictrs/image/f3440adf.png",
            "ap_id": "https://lemmy.world/post/50908658",
            "local": true,
            "language_id": 37,
            "featured_community": false,
            "featured_local": false
          },
          "creator": {
            "id": 306405,
            "name": "cannedtuna",
            "display_name": "Canned Tuna",
            "avatar": "https://lemmy.world/pictrs/image/504937c6.jpeg",
            "banned": false,
            "published": "2023-06-21T09:22:34.462094Z",
            "actor_id": "https://lemmy.world/u/cannedtuna",
            "local": true,
            "deleted": false,
            "bot_account": false,
            "instance_id": 1
          },
          "community": {
            "id": 79185,
            "name": "microblogmemes",
            "title": "Microblog Memes",
            "description": "A place to share screenshots.",
            "removed": false,
            "published": "2023-07-08T11:42:21.747619Z",
            "deleted": false,
            "nsfw": false,
            "actor_id": "https://lemmy.world/c/microblogmemes",
            "local": true,
            "hidden": false,
            "posting_restricted_to_mods": false,
            "instance_id": 1
          },
          "creator_is_moderator": false,
          "creator_is_admin": true,
          "counts": {
            "post_id": 50908658,
            "comments": 285,
            "score": 718,
            "upvotes": 746,
            "downvotes": 28,
            "published": "2026-08-20T20:31:39.106789Z",
            "newest_comment_time": "2026-08-21T17:27:46.442995Z"
          }
        }
      ],
      "next_page": "P308c595"
    }
    """;

    /// <summary>A page whose only post is missing its actor id, which is not something we can map.</summary>
    internal const string PostListWithOneUnmappablePost = """
    {
      "posts": [
        {
          "post": {
            "id": 1, "name": "Fine", "creator_id": 1, "community_id": 1,
            "ap_id": "https://lemmy.world/post/1", "published": "2026-08-20T20:31:39Z",
            "removed": false, "locked": false, "deleted": false, "nsfw": false,
            "language_id": 0, "featured_community": false, "featured_local": false
          },
          "creator": {
            "id": 1, "name": "alice", "actor_id": "https://lemmy.world/u/alice",
            "published": "2023-01-01T00:00:00Z", "local": true, "banned": false,
            "deleted": false, "bot_account": false, "instance_id": 1
          },
          "community": {
            "id": 1, "name": "test", "title": "Test",
            "actor_id": "https://lemmy.world/c/test", "published": "2023-01-01T00:00:00Z",
            "local": true, "removed": false, "deleted": false, "nsfw": false,
            "hidden": false, "posting_restricted_to_mods": false, "instance_id": 1
          },
          "creator_is_moderator": false,
          "creator_is_admin": false,
          "counts": { "post_id": 1, "comments": 0, "score": 0, "upvotes": 0, "downvotes": 0 }
        },
        {
          "post": {
            "id": 2, "name": "Broken", "creator_id": 1, "community_id": 1,
            "published": "2026-08-20T20:31:39Z",
            "removed": false, "locked": false, "deleted": false, "nsfw": false,
            "language_id": 0, "featured_community": false, "featured_local": false
          },
          "creator": {
            "id": 1, "name": "alice", "actor_id": "https://lemmy.world/u/alice",
            "published": "2023-01-01T00:00:00Z", "local": true, "banned": false,
            "deleted": false, "bot_account": false, "instance_id": 1
          },
          "community": {
            "id": 1, "name": "test", "title": "Test",
            "actor_id": "https://lemmy.world/c/test", "published": "2023-01-01T00:00:00Z",
            "local": true, "removed": false, "deleted": false, "nsfw": false,
            "hidden": false, "posting_restricted_to_mods": false, "instance_id": 1
          },
          "creator_is_moderator": false,
          "creator_is_admin": false,
          "counts": { "post_id": 2, "comments": 0, "score": 0, "upvotes": 0, "downvotes": 0 }
        }
      ]
    }
    """;

    /// <summary>Three comments: a root, its reply, and a second root — deliberately out of tree order.</summary>
    internal const string CommentList = """
    {
      "comments": [
        {
          "comment": {
            "id": 200, "creator_id": 1, "post_id": 10, "content": "A reply",
            "path": "0.100.200", "ap_id": "https://lemmy.world/comment/200",
            "published": "2026-08-21T11:00:00Z", "removed": false, "deleted": false,
            "distinguished": false, "local": true, "language_id": 0
          },
          "creator": {
            "id": 1, "name": "bob", "actor_id": "https://lemmy.world/u/bob",
            "published": "2023-01-01T00:00:00Z", "local": true, "banned": false,
            "deleted": false, "bot_account": false, "instance_id": 1
          },
          "creator_is_moderator": false,
          "creator_is_admin": false,
          "counts": { "comment_id": 200, "score": 4, "upvotes": 4, "downvotes": 0, "child_count": 0 }
        },
        {
          "comment": {
            "id": 100, "creator_id": 1, "post_id": 10, "content": "A root",
            "path": "0.100", "ap_id": "https://lemmy.world/comment/100",
            "published": "2026-08-21T10:00:00Z", "removed": false, "deleted": false,
            "distinguished": false, "local": true, "language_id": 0
          },
          "creator": {
            "id": 1, "name": "alice", "actor_id": "https://lemmy.world/u/alice",
            "published": "2023-01-01T00:00:00Z", "local": true, "banned": false,
            "deleted": false, "bot_account": false, "instance_id": 1
          },
          "creator_is_moderator": true,
          "creator_is_admin": false,
          "counts": { "comment_id": 100, "score": 9, "upvotes": 9, "downvotes": 0, "child_count": 3 }
        },
        {
          "comment": {
            "id": 300, "creator_id": 1, "post_id": 10, "content": "Another root",
            "path": "0.300", "ap_id": "https://lemmy.world/comment/300",
            "published": "2026-08-21T12:00:00Z", "removed": false, "deleted": false,
            "distinguished": false, "local": true, "language_id": 0
          },
          "creator": {
            "id": 1, "name": "carol", "actor_id": "https://lemmy.world/u/carol",
            "published": "2023-01-01T00:00:00Z", "local": true, "banned": false,
            "deleted": false, "bot_account": false, "instance_id": 1
          },
          "creator_is_moderator": false,
          "creator_is_admin": false,
          "counts": { "comment_id": 300, "score": 1, "upvotes": 1, "downvotes": 0, "child_count": 0 }
        }
      ]
    }
    """;

    /// <summary>A sub-thread, as "load more replies" returns it: the top item's parent is not present.</summary>
    internal const string CommentSubThread = """
    {
      "comments": [
        {
          "comment": {
            "id": 400, "creator_id": 1, "post_id": 10, "content": "Deep",
            "path": "0.100.200.400", "ap_id": "https://lemmy.world/comment/400",
            "published": "2026-08-21T11:30:00Z", "removed": false, "deleted": false,
            "distinguished": false, "local": true, "language_id": 0
          },
          "creator": {
            "id": 1, "name": "dave", "actor_id": "https://lemmy.world/u/dave",
            "published": "2023-01-01T00:00:00Z", "local": true, "banned": false,
            "deleted": false, "bot_account": false, "instance_id": 1
          },
          "creator_is_moderator": false,
          "creator_is_admin": false,
          "counts": { "comment_id": 400, "score": 2, "upvotes": 2, "downvotes": 0, "child_count": 0 }
        }
      ]
    }
    """;

    /// <summary>Lemmy builds older than 0.19.4 omit the trailing Z on timestamps.</summary>
    internal const string PostListWithUnlabelledTimestamp = """
    {
      "posts": [
        {
          "post": {
            "id": 1, "name": "Old build", "creator_id": 1, "community_id": 1,
            "ap_id": "https://old.example.com/post/1",
            "published": "2026-08-20T20:31:39.106789",
            "removed": false, "locked": false, "deleted": false, "nsfw": false,
            "language_id": 0, "featured_community": false, "featured_local": false
          },
          "creator": {
            "id": 1, "name": "alice", "actor_id": "https://old.example.com/u/alice",
            "published": "2023-01-01T00:00:00", "local": true, "banned": false,
            "deleted": false, "bot_account": false, "instance_id": 1
          },
          "community": {
            "id": 1, "name": "test", "title": "Test",
            "actor_id": "https://old.example.com/c/test", "published": "2023-01-01T00:00:00",
            "local": true, "removed": false, "deleted": false, "nsfw": false,
            "hidden": false, "posting_restricted_to_mods": false, "instance_id": 1
          },
          "creator_is_moderator": false,
          "creator_is_admin": false,
          "counts": { "post_id": 1, "comments": 0, "score": 0, "upvotes": 0, "downvotes": 0 }
        }
      ]
    }
    """;

    internal const string Site = """
    {
      "site_view": {
        "site": {
          "id": 1,
          "name": "Lemmy.World",
          "sidebar": "The World's Internet Frontpage",
          "description": "A general-purpose instance",
          "icon": "https://lemmy.world/pictrs/image/icon.png",
          "actor_id": "https://lemmy.world/"
        },
        "counts": { "users": 197152, "posts": 759238, "comments": 6919916, "communities": 13572 }
      },
      "version": "0.19.19-9-gc55dd700c"
    }
    """;

    internal const string CommunityList = """
    {
      "communities": [
        {
          "community": {
            "id": 2478, "name": "technology", "title": "Technology",
            "description": "Tech news and articles.",
            "actor_id": "https://lemmy.world/c/technology",
            "published": "2023-06-11T02:16:17.173483Z",
            "local": true, "removed": false, "deleted": false, "nsfw": false,
            "hidden": false, "posting_restricted_to_mods": false, "instance_id": 1
          },
          "counts": {
            "community_id": 2478, "subscribers": 87409, "posts": 21286,
            "comments": 932665, "users_active_month": 15822
          }
        }
      ]
    }
    """;

    internal const string ErrorBody = """{"error":"couldnt_find_post"}""";
}
