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

    /// <summary>Fetches one page of a community listing.</summary>
    /// <exception cref="LemmyApiException">The server refused the request or sent something unusable.</exception>
    Task<ImmutableArray<CommunitySummary>> GetCommunitiesAsync(CommunityQuery query, CancellationToken cancellationToken = default);

    /// <summary>Searches the instance.</summary>
    /// <exception cref="LemmyApiException">The server refused the request or sent something unusable.</exception>
    Task<SearchResults> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>Fetches the instance's own description and totals.</summary>
    /// <exception cref="LemmyApiException">The server refused the request or sent something unusable.</exception>
    Task<SiteSummary> GetSiteAsync(CancellationToken cancellationToken = default);
}
