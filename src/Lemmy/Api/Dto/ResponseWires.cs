using System.Collections.Immutable;

namespace Lemmy.Api.Dto;

/// <summary>The body of <c>GET /api/v3/post/list</c>.</summary>
internal sealed record GetPostsResponse
{
    public ImmutableArray<PostViewWire> Posts { get; init; } = [];

    /// <summary>The cursor for the next page; absent at the end of the feed.</summary>
    public string? NextPage { get; init; }
}

/// <summary>The body of <c>GET /api/v3/post</c>.</summary>
internal sealed record GetPostResponse
{
    public PostViewWire? PostView { get; init; }
}

/// <summary>The body of <c>GET /api/v3/comment/list</c>.</summary>
internal sealed record GetCommentsResponse
{
    public ImmutableArray<CommentViewWire> Comments { get; init; } = [];
}

/// <summary>The body of <c>GET /api/v3/community/list</c>.</summary>
internal sealed record ListCommunitiesResponse
{
    public ImmutableArray<CommunityViewWire> Communities { get; init; } = [];
}

/// <summary>The body of <c>GET /api/v3/search</c>.</summary>
internal sealed record SearchResponse
{
    public ImmutableArray<PostViewWire> Posts { get; init; } = [];

    public ImmutableArray<CommentViewWire> Comments { get; init; } = [];

    public ImmutableArray<CommunityViewWire> Communities { get; init; } = [];

    public ImmutableArray<PersonViewWire> Users { get; init; } = [];
}

/// <summary>An account together with its counts, as search results and its own page carry it.</summary>
internal sealed record PersonViewWire
{
    public PersonWire? Person { get; init; }

    public PersonAggregatesWire? Counts { get; init; }

    public bool IsAdmin { get; init; }
}

/// <summary>The body of <c>GET /api/v3/user</c>.</summary>
internal sealed record GetPersonDetailsResponse
{
    public PersonViewWire? PersonView { get; init; }

    public ImmutableArray<PostViewWire> Posts { get; init; } = [];

    public ImmutableArray<CommentViewWire> Comments { get; init; } = [];
}

/// <summary>The body of <c>GET /api/v3/site</c>.</summary>
internal sealed record GetSiteResponse
{
    public SiteViewWire? SiteView { get; init; }

    /// <summary>
    /// Present only when the request carried a valid token. Its absence is how a dead session is
    /// detected: Lemmy answers 200 and simply omits it rather than rejecting the request.
    /// </summary>
    public MyUserInfoWire? MyUser { get; init; }

    /// <summary>The Lemmy build the instance runs, e.g. <c>0.19.19</c>.</summary>
    public string? Version { get; init; }
}

/// <summary>The body of <c>POST /api/v3/user/login</c>.</summary>
internal sealed record LoginResponse
{
    /// <summary>The bearer token, absent when the account needs a further step.</summary>
    public string? Jwt { get; init; }

    /// <summary>Set when the instance requires an admin to approve the registration first.</summary>
    public bool RegistrationCreated { get; init; }

    /// <summary>Set when the instance wants the email address confirmed first.</summary>
    public bool VerifyEmailSent { get; init; }
}

/// <summary>The signed-in account, as <c>GET /api/v3/site</c> reports it once authenticated.</summary>
internal sealed record MyUserInfoWire
{
    public LocalUserViewWire? LocalUserView { get; init; }
}

/// <summary>The account row inside <c>my_user</c>.</summary>
internal sealed record LocalUserViewWire
{
    public PersonWire? Person { get; init; }

    public LocalUserWire? LocalUser { get; init; }
}

/// <summary>
/// The account's own settings, as chosen on the instance itself. Only the two that decide what this
/// app shows; Lemmy carries a couple of dozen more that belong to its own frontend.
/// </summary>
internal sealed record LocalUserWire
{
    public bool ShowNsfw { get; init; }

    public bool BlurNsfw { get; init; } = true;
}

/// <summary>The body Lemmy returns for a 4xx or 5xx, when it returns one at all.</summary>
internal sealed record ErrorResponse
{
    public string? Error { get; init; }

    public string? Message { get; init; }
}

/// <summary>The body of <c>POST /api/v3/post/like</c>.</summary>
internal sealed record PostResponse
{
    public PostViewWire? PostView { get; init; }
}

/// <summary>The body of <c>POST /api/v3/comment/like</c>.</summary>
internal sealed record CommentResponse
{
    public CommentViewWire? CommentView { get; init; }
}

/// <summary>The body sent to <c>POST /api/v3/post</c>.</summary>
internal sealed record CreatePostRequestWire
{
    /// <summary>Lemmy calls the title "name".</summary>
    public string? Name { get; init; }

    public int CommunityId { get; init; }

    /// <summary>Omitted when the post links nowhere; an empty string would be refused as a bad URL.</summary>
    public string? Url { get; init; }

    public string? Body { get; init; }

    public bool Nsfw { get; init; }
}

/// <summary>The body sent to <c>PUT /api/v3/post</c>.</summary>
/// <remarks>
/// Unlike the create request, the optional fields are sent even when empty. Lemmy reads a missing
/// field as "leave this one alone" and an empty one as "clear it", so omitting them would make
/// removing a link or a body impossible rather than merely awkward.
/// </remarks>
internal sealed record EditPostRequestWire
{
    public int PostId { get; init; }

    public string? Name { get; init; }

    public string? Url { get; init; }

    public string? Body { get; init; }

    public bool Nsfw { get; init; }
}

/// <summary>The body sent to <c>POST /api/v3/post/delete</c>.</summary>
internal sealed record DeletePostRequestWire
{
    public int PostId { get; init; }

    public bool Deleted { get; init; }
}

/// <summary>The body sent to <c>POST /api/v3/comment</c>.</summary>
internal sealed record CreateCommentRequestWire
{
    public string? Content { get; init; }

    public int PostId { get; init; }

    /// <summary>The comment being replied to; absent for a reply to the post itself.</summary>
    public int? ParentId { get; init; }
}

/// <summary>The body sent to <c>PUT /api/v3/comment</c>.</summary>
internal sealed record EditCommentRequestWire
{
    public int CommentId { get; init; }

    public string? Content { get; init; }
}

/// <summary>The body sent to <c>POST /api/v3/comment/delete</c>.</summary>
internal sealed record DeleteCommentRequestWire
{
    public int CommentId { get; init; }

    public bool Deleted { get; init; }
}

/// <summary>The body of <c>POST /api/v3/community/follow</c>.</summary>
internal sealed record CommunityResponse
{
    public CommunityViewWire? CommunityView { get; init; }
}

/// <summary>The body sent to <c>POST /api/v3/community/follow</c>.</summary>
internal sealed record FollowCommunityRequestWire
{
    public int CommunityId { get; init; }

    public bool Follow { get; init; }
}

/// <summary>The body sent to <c>POST /api/v3/post/like</c>.</summary>
internal sealed record VotePostRequestWire
{
    public int PostId { get; init; }

    /// <summary>1, 0 or -1; zero is how a vote is taken back.</summary>
    public int Score { get; init; }
}

/// <summary>The body sent to <c>POST /api/v3/comment/like</c>.</summary>
internal sealed record VoteCommentRequestWire
{
    public int CommentId { get; init; }

    /// <summary>1, 0 or -1; zero is how a vote is taken back.</summary>
    public int Score { get; init; }
}

/// <summary>The body sent to <c>POST /api/v3/user/login</c>.</summary>
internal sealed record LoginRequestWire
{
    public string? UsernameOrEmail { get; init; }

    public string? Password { get; init; }

    public string? Totp2faToken { get; init; }
}

/// <summary>The body of <c>GET /api/v3/user/unread_count</c>.</summary>
internal sealed record GetUnreadCountResponse
{
    public int Replies { get; init; }

    public int Mentions { get; init; }

    public int PrivateMessages { get; init; }
}

/// <summary>
/// The row that makes a comment a notification. Lemmy has two of these — <c>comment_reply</c> and
/// <c>person_mention</c> — and their fields are identical, so one record reads both and which one
/// arrived is what says whether it was a reply or a mention.
/// </summary>
internal sealed record NotificationMarkerWire
{
    public int Id { get; init; }

    public int RecipientId { get; init; }

    public int CommentId { get; init; }

    public bool Read { get; init; }

    public DateTimeOffset? Published { get; init; }
}

/// <summary>
/// A notification as both of Lemmy's lists carry it: a comment with its post and community joined
/// on, plus whichever marker row addressed it to this account.
/// </summary>
internal sealed record NotificationViewWire
{
    public NotificationMarkerWire? CommentReply { get; init; }

    public NotificationMarkerWire? PersonMention { get; init; }

    public CommentWire? Comment { get; init; }

    public PersonWire? Creator { get; init; }

    public PostWire? Post { get; init; }

    public CommunityWire? Community { get; init; }

    public CommentAggregatesWire? Counts { get; init; }

    /// <summary>How the signed-in account voted. Absent entirely when nobody is signed in.</summary>
    public int? MyVote { get; init; }

    public bool CreatorIsModerator { get; init; }

    public bool CreatorIsAdmin { get; init; }
}

/// <summary>The body of <c>GET /api/v3/user/replies</c>.</summary>
internal sealed record GetRepliesResponse
{
    public ImmutableArray<NotificationViewWire> Replies { get; init; } = [];
}

/// <summary>The body of <c>GET /api/v3/user/mention</c>.</summary>
internal sealed record GetPersonMentionsResponse
{
    public ImmutableArray<NotificationViewWire> Mentions { get; init; } = [];
}

/// <summary>The body of <c>POST /api/v3/comment/mark_as_read</c>.</summary>
internal sealed record CommentReplyResponse
{
    public NotificationViewWire? CommentReplyView { get; init; }
}

/// <summary>The body of <c>POST /api/v3/user/mention/mark_as_read</c>.</summary>
internal sealed record PersonMentionResponse
{
    public NotificationViewWire? PersonMentionView { get; init; }
}

/// <summary>The body of <c>POST /api/v3/comment/mark_as_read</c>.</summary>
internal sealed record MarkCommentReplyReadRequestWire
{
    public required int CommentReplyId { get; init; }

    public required bool Read { get; init; }
}

/// <summary>The body of <c>POST /api/v3/user/mention/mark_as_read</c>.</summary>
internal sealed record MarkPersonMentionReadRequestWire
{
    public required int PersonMentionId { get; init; }

    public required bool Read { get; init; }
}

/// <summary>
/// The body of <c>POST /api/v3/user/mark_all_as_read</c>, which takes nothing at all. Sent as an
/// empty object rather than as no body, because the endpoint still expects JSON.
/// </summary>
internal sealed record MarkAllReadRequestWire;

