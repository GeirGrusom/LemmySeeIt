using System.Collections.Immutable;
using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Api;

/// <summary>
/// Read access to one Lemmy instance. Bound to a single <see cref="Instance"/>: switching servers
/// means a new client, which keeps instance-local identifiers from ever crossing servers.
/// </summary>
public interface ILemmyApi
{
    /// <summary>The instance every call on this client goes to.</summary>
    InstanceAddress Instance { get; }

    /// <summary>Whether this client carries a session, and so sees the reader's own votes and subscriptions.</summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Signs in and returns the bearer token. The password is used for this one request and is not
    /// retained anywhere.
    /// </summary>
    /// <exception cref="LemmyApiException">
    /// The credentials were refused, the account needs a second factor, or the instance is holding
    /// the registration for approval or email confirmation.
    /// </exception>
    Task<SessionToken> LogInAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells the instance to invalidate this session. Worth doing rather than merely forgetting the
    /// token locally: Lemmy tokens do not expire, so a forgotten one stays usable indefinitely.
    /// </summary>
    Task LogOutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The signed-in account, or <see langword="null"/> when this client has no working session.
    /// Also the way to tell that a stored token has stopped working: Lemmy answers normally and
    /// simply omits the account rather than rejecting the request.
    /// </summary>
    Task<Account?> GetMyAccountAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches one page of a feed.</summary>
    /// <exception cref="LemmyApiException">The server refused the request or sent something unusable.</exception>
    Task<PostPage> GetFeedAsync(FeedQuery query, CancellationToken cancellationToken = default);

    /// <summary>Fetches a single post with its creator, community and counts.</summary>
    /// <exception cref="LemmyApiException">The post does not exist, or the server refused the request.</exception>
    Task<PostSummary> GetPostAsync(PostId postId, CancellationToken cancellationToken = default);

    /// <summary>Fetches a post's comments, already assembled into trees.</summary>
    /// <exception cref="LemmyApiException">The server refused the request or sent something unusable.</exception>
    Task<CommentThread> GetCommentsAsync(CommentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// How much is waiting for the signed-in account. Answers an empty tally rather than refusing
    /// when nobody is signed in: the shell asks this on every launch, and being signed out is an
    /// ordinary answer to the question, not an error.
    /// </summary>
    Task<UnreadTally> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The replies and mentions addressed to the signed-in account, newest first. Lemmy keeps these
    /// in two separate lists; this fetches both and merges them, because the reader is owed one
    /// list in time order rather than two they have to interleave in their head.
    /// </summary>
    Task<ImmutableArray<Notification>> GetNotificationsAsync(
        NotificationQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Marks one notification read, or puts it back to unread.</summary>
    /// <param name="kind">Which list it came from; the two are marked read through different endpoints.</param>
    /// <param name="id">Its row in that list.</param>
    /// <param name="read">Whether it should now count as seen.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<Notification> SetNotificationReadAsync(
        NotificationKind kind,
        NotificationId id,
        bool read,
        CancellationToken cancellationToken = default);

    /// <summary>Marks everything waiting as read, which is one request rather than one per row.</summary>
    Task MarkEverythingReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches one page of a community listing.</summary>
    /// <exception cref="LemmyApiException">The server refused the request or sent something unusable.</exception>
    Task<ImmutableArray<CommunitySummary>> GetCommunitiesAsync(CommunityQuery query, CancellationToken cancellationToken = default);

    /// <summary>Searches the instance.</summary>
    /// <exception cref="LemmyApiException">The server refused the request or sent something unusable.</exception>
    Task<SearchResults> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Casts, changes or takes back a vote on a post. <see cref="Vote.None"/> takes it back.
    /// </summary>
    /// <exception cref="LemmyApiException">
    /// Nobody is signed in, the session has stopped working, or the instance refused the vote —
    /// which it does for a locked post or from a community the account is banned from.
    /// </exception>
    Task<VoteOutcome> VoteOnPostAsync(PostId postId, Vote vote, CancellationToken cancellationToken = default);

    /// <summary>
    /// Casts, changes or takes back a vote on a comment. <see cref="Vote.None"/> takes it back.
    /// </summary>
    /// <exception cref="LemmyApiException">
    /// Nobody is signed in, the session has stopped working, or the instance refused the vote.
    /// </exception>
    Task<VoteOutcome> VoteOnCommentAsync(CommentId commentId, Vote vote, CancellationToken cancellationToken = default);

    /// <summary>
    /// Follows or unfollows a community, and answers with where the account now stands. Following a
    /// remote community usually comes back <see cref="SubscriptionState.Pending"/> rather than
    /// subscribed: the follow has to travel to the community's own instance and be acknowledged.
    /// </summary>
    /// <exception cref="LemmyApiException">
    /// Nobody is signed in, the session has stopped working, or the instance refused — which it does
    /// for a community the account is banned from.
    /// </exception>
    Task<SubscriptionState> SetSubscriptionAsync(
        CommunityId communityId,
        bool follow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts something new to a community, and answers with the post as the feed would have sent it.
    /// </summary>
    /// <param name="community">Where it goes; a post cannot be moved afterwards.</param>
    /// <param name="draft">What it says.</param>
    /// <param name="cancellationToken">Abandons the request.</param>
    /// <exception cref="LemmyApiException">
    /// Nobody is signed in, the community does not take posts from this account, or the instance
    /// sent back something unusable.
    /// </exception>
    Task<PostSummary> CreatePostAsync(
        CommunityId community,
        PostDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rewrites a post. Everything the draft carries is sent, including the parts left empty, so
    /// that a link or a body can be taken away and not merely changed.
    /// </summary>
    /// <exception cref="LemmyApiException">
    /// Nobody is signed in, the post is not the account's, or the instance refused the edit.
    /// </exception>
    Task<PostSummary> EditPostAsync(
        PostId postId,
        PostDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a post, or restores one already deleted. As with comments, Lemmy's delete is a flag
    /// rather than a removal, which is what makes putting it back possible.
    /// </summary>
    /// <exception cref="LemmyApiException">
    /// Nobody is signed in, the post is not the account's, or the instance refused.
    /// </exception>
    Task<PostSummary> SetPostDeletedAsync(
        PostId postId,
        bool deleted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a comment, either on the post itself or as a reply to another comment.
    /// </summary>
    /// <param name="postId">The post being commented on.</param>
    /// <param name="parentId">The comment being replied to, or <see langword="null"/> for the post.</param>
    /// <param name="draft">What to say.</param>
    /// <param name="cancellationToken">Abandons the request.</param>
    /// <exception cref="LemmyApiException">
    /// Nobody is signed in, the post is locked, the account is banned from the community, or the
    /// instance sent back something unusable.
    /// </exception>
    Task<CommentNode> CreateCommentAsync(
        PostId postId,
        CommentId? parentId,
        CommentDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rewrites a comment. Answers with the comment alone rather than a node: the replies below it
    /// are unchanged and the response does not carry them.
    /// </summary>
    /// <exception cref="LemmyApiException">
    /// Nobody is signed in, the comment is not the account's, or the instance refused the edit.
    /// </exception>
    Task<Comment> EditCommentAsync(
        CommentId commentId,
        CommentDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a comment, or restores one already deleted. Lemmy's delete is a flag rather than a
    /// removal — the comment keeps its place in the thread so the replies below it still hang off
    /// something — which is what makes restoring possible at all.
    /// </summary>
    /// <exception cref="LemmyApiException">
    /// Nobody is signed in, the comment is not the account's, or the instance refused.
    /// </exception>
    Task<Comment> SetCommentDeletedAsync(
        CommentId commentId,
        bool deleted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches an account's page: who they are and the most recent of what they have written.
    /// </summary>
    /// <exception cref="LemmyApiException">
    /// The account does not exist on this instance, or the server refused the request.
    /// </exception>
    Task<PersonProfile> GetPersonAsync(PersonId personId, CancellationToken cancellationToken = default);

    /// <summary>Fetches the instance's own description and totals.</summary>
    /// <exception cref="LemmyApiException">The server refused the request or sent something unusable.</exception>
    Task<SiteSummary> GetSiteAsync(CancellationToken cancellationToken = default);
}
