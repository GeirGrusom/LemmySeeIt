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

/// <summary>An account together with its counts, as search results carry it.</summary>
internal sealed record PersonViewWire
{
    public PersonWire? Person { get; init; }
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
