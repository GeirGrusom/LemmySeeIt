using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Lemmy.Api.Dto;
using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Api;

/// <summary>
/// Talks to Lemmy's HTTP API v3 — the version every current instance serves. All requests are
/// anonymous reads: this client never sends credentials and has no endpoint that writes.
/// </summary>
public sealed class LemmyApiClient : ILemmyApi
{
    private const string ApiRoot = "api/v3/";

    /// <summary>Enough room for every query this client builds, so growth stays off the hot path.</summary>
    private const int QueryBufferLength = 320;

    private readonly HttpClient httpClient;
    private readonly SessionToken session;

    /// <summary>Points a client at an instance.</summary>
    /// <param name="httpClient">The transport; its lifetime belongs to the caller.</param>
    /// <param name="instance">The server to read from.</param>
    /// <param name="session">A session to act as, or <see langword="default"/> to read anonymously.</param>
    /// <exception cref="ArgumentException"><paramref name="instance"/> is <see langword="default"/>.</exception>
    public LemmyApiClient(HttpClient httpClient, InstanceAddress instance, SessionToken session = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (!instance.IsValid)
        {
            throw new ArgumentException("A Lemmy client needs a real instance address.", nameof(instance));
        }

        this.httpClient = httpClient;
        this.session = session;
        Instance = instance;
    }

    /// <inheritdoc />
    public InstanceAddress Instance { get; }

    /// <inheritdoc />
    public bool IsAuthenticated => session.IsValid;

    /// <summary>
    /// Configures a transport for Lemmy: a decompressing handler, a polite user agent and a timeout
    /// short enough that a wedged instance shows an error instead of an endless spinner.
    /// </summary>
    public static HttpClient CreateHttpClient(string userAgent, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);

        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        var client = new HttpClient(handler)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    /// <inheritdoc />
    public async Task<PostPage> GetFeedAsync(FeedQuery query, CancellationToken cancellationToken = default)
    {
        string endpoint;
        using (var builder = new QueryStringBuilder(stackalloc char[QueryBufferLength]))
        {
            builder.Append("type_", query.Listing.ToWire());
            builder.Append("sort", query.Sort.ToWire());
            builder.Append("limit", query.PageSize);

            if (query.Community is { } community)
            {
                builder.Append("community_id", community);
            }

            if (query.Cursor is { } cursor)
            {
                builder.Append("page_cursor", cursor.Value);
            }

            builder.Append("show_nsfw", query.ShowNsfw);
            endpoint = Endpoint("post/list", builder.Span);
        }

        GetPostsResponse response = await GetAsync(endpoint, LemmyJson.Context.GetPostsResponse, cancellationToken)
            .ConfigureAwait(false);

        PageCursor? next = PageCursor.TryCreate(response.NextPage.AsSpan(), out PageCursor cursorValue) ? cursorValue : null;

        return new PostPage(WireMapper.MapPostSummaries(response.Posts), next);
    }

    /// <inheritdoc />
    public async Task<PostSummary> GetPostAsync(PostId postId, CancellationToken cancellationToken = default)
    {
        string endpoint;
        using (var builder = new QueryStringBuilder(stackalloc char[QueryBufferLength]))
        {
            builder.Append("id", postId);
            endpoint = Endpoint("post", builder.Span);
        }

        GetPostResponse response = await GetAsync(endpoint, LemmyJson.Context.GetPostResponse, cancellationToken)
            .ConfigureAwait(false);

        if (!WireMapper.TryMapPostSummary(response.PostView, out PostSummary summary))
        {
            throw new LemmyApiException(
                $"Post {postId} came back in a shape this client cannot read.",
                endpoint,
                HttpStatusCode.OK);
        }

        return summary;
    }

    /// <inheritdoc />
    public async Task<CommentThread> GetCommentsAsync(CommentQuery query, CancellationToken cancellationToken = default)
    {
        string endpoint;
        using (var builder = new QueryStringBuilder(stackalloc char[QueryBufferLength]))
        {
            builder.Append("post_id", query.Post);
            builder.Append("sort", query.Sort.ToWire());
            builder.Append("max_depth", query.MaxDepth);
            builder.Append("limit", query.PageSize);
            builder.Append("type_", ListingType.All.ToWire());

            if (query.Parent is { } parent)
            {
                builder.Append("parent_id", parent);
            }

            endpoint = Endpoint("comment/list", builder.Span);
        }

        GetCommentsResponse response = await GetAsync(endpoint, LemmyJson.Context.GetCommentsResponse, cancellationToken)
            .ConfigureAwait(false);

        return CommentTreeBuilder.Build(response.Comments);
    }

    /// <inheritdoc />
    public async Task<ImmutableArray<CommunitySummary>> GetCommunitiesAsync(
        CommunityQuery query,
        CancellationToken cancellationToken = default)
    {
        string endpoint;
        using (var builder = new QueryStringBuilder(stackalloc char[QueryBufferLength]))
        {
            builder.Append("type_", query.Listing.ToWire());
            builder.Append("sort", query.Sort.ToWire());
            builder.Append("limit", query.PageSize);
            builder.Append("page", query.PageNumber.ToString(CultureInfo.InvariantCulture));
            builder.Append("show_nsfw", query.ShowNsfw);
            endpoint = Endpoint("community/list", builder.Span);
        }

        ListCommunitiesResponse response =
            await GetAsync(endpoint, LemmyJson.Context.ListCommunitiesResponse, cancellationToken).ConfigureAwait(false);

        return WireMapper.MapCommunitySummaries(response.Communities);
    }

    /// <inheritdoc />
    public async Task<SearchResults> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        if (!query.Term.IsValid)
        {
            return SearchResults.Empty;
        }

        string endpoint;
        using (var builder = new QueryStringBuilder(stackalloc char[QueryBufferLength]))
        {
            builder.Append("q", query.Term.Value);
            builder.Append("type_", query.Kind.ToWire());
            builder.Append("listing_type", query.Listing.ToWire());
            builder.Append("sort", query.Sort.ToWire());
            builder.Append("limit", query.PageSize);
            builder.Append("page", query.PageNumber.ToString(CultureInfo.InvariantCulture));

            if (query.Community is { } community)
            {
                builder.Append("community_id", community);
            }

            endpoint = Endpoint("search", builder.Span);
        }

        SearchResponse response = await GetAsync(endpoint, LemmyJson.Context.SearchResponse, cancellationToken)
            .ConfigureAwait(false);

        return new SearchResults(
            WireMapper.MapPostSummaries(response.Posts),
            WireMapper.MapCommunitySummaries(response.Communities),
            WireMapper.MapPeople(response.Users));
    }

    /// <inheritdoc />
    public async Task<SessionToken> LogInAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.IsComplete)
        {
            throw new LemmyApiException("Sign-in needs a username and a password.");
        }

        var body = new LoginRequestWire
        {
            UsernameOrEmail = request.UsernameOrEmail.Trim(),
            Password = request.Password,
            Totp2faToken = string.IsNullOrWhiteSpace(request.TotpToken) ? null : request.TotpToken.Trim(),
        };

        LoginResponse response = await PostAsync(
            ApiRoot + "user/login",
            body,
            LemmyJson.Context.LoginRequestWire,
            LemmyJson.Context.LoginResponse,
            cancellationToken).ConfigureAwait(false);

        if (SessionToken.TryCreate(response.Jwt.AsSpan(), out SessionToken token))
        {
            return token;
        }

        // A sign-in that succeeds without a token means the instance wants something else first.
        throw new LemmyApiException(
            response switch
            {
                { VerifyEmailSent: true } => $"{Instance.Value} wants the email address on this account confirmed first.",
                { RegistrationCreated: true } => $"{Instance.Value} is holding this registration for an admin to approve.",
                _ => $"{Instance.Value} accepted the sign-in but did not issue a token.",
            });
    }

    /// <inheritdoc />
    public async Task LogOutAsync(CancellationToken cancellationToken = default)
    {
        if (!session.IsValid)
        {
            return;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Instance.BaseUri, ApiRoot + "user/logout"));
        Authorise(request);

        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            // Signing out locally still has to happen, so a failure here is not worth raising: the
            // caller is discarding the token either way.
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    /// <inheritdoc />
    public async Task<Account?> GetMyAccountAsync(CancellationToken cancellationToken = default)
    {
        if (!session.IsValid)
        {
            return null;
        }

        GetSiteResponse response = await GetAsync(ApiRoot + "site", LemmyJson.Context.GetSiteResponse, cancellationToken)
            .ConfigureAwait(false);

        return WireMapper.TryMapAccount(response, Instance, out Account account) ? account : null;
    }

    /// <inheritdoc />
    public async Task<VoteOutcome> VoteOnPostAsync(
        PostId postId,
        Vote vote,
        CancellationToken cancellationToken = default)
    {
        RequireSession("vote");

        PostResponse response = await PostAsync(
            ApiRoot + "post/like",
            new VotePostRequestWire { PostId = postId.Value, Score = vote.ToScore() },
            LemmyJson.Context.VotePostRequestWire,
            LemmyJson.Context.PostResponse,
            cancellationToken).ConfigureAwait(false);

        return WireMapper.MapVoteOutcome(response.PostView);
    }

    /// <inheritdoc />
    public async Task<VoteOutcome> VoteOnCommentAsync(
        CommentId commentId,
        Vote vote,
        CancellationToken cancellationToken = default)
    {
        RequireSession("vote");

        CommentResponse response = await PostAsync(
            ApiRoot + "comment/like",
            new VoteCommentRequestWire { CommentId = commentId.Value, Score = vote.ToScore() },
            LemmyJson.Context.VoteCommentRequestWire,
            LemmyJson.Context.CommentResponse,
            cancellationToken).ConfigureAwait(false);

        return WireMapper.MapVoteOutcome(response.CommentView);
    }

    /// <inheritdoc />
    public async Task<SubscriptionState> SetSubscriptionAsync(
        CommunityId communityId,
        bool follow,
        CancellationToken cancellationToken = default)
    {
        RequireSession("subscribe");

        CommunityResponse response = await PostAsync(
            ApiRoot + "community/follow",
            new FollowCommunityRequestWire { CommunityId = communityId.Value, Follow = follow },
            LemmyJson.Context.FollowCommunityRequestWire,
            LemmyJson.Context.CommunityResponse,
            cancellationToken).ConfigureAwait(false);

        return response.CommunityView?.Subscribed.ToSubscriptionState() ?? SubscriptionState.NotSubscribed;
    }

    /// <inheritdoc />
    public async Task<CommentNode> CreateCommentAsync(
        PostId postId,
        CommentId? parentId,
        CommentDraft draft,
        CancellationToken cancellationToken = default)
    {
        RequireSession("comment");
        RequireDraft(draft);

        CommentResponse response = await PostAsync(
            ApiRoot + "comment",
            new CreateCommentRequestWire
            {
                Content = draft.Value,
                PostId = postId.Value,
                ParentId = parentId?.Value,
            },
            LemmyJson.Context.CreateCommentRequestWire,
            LemmyJson.Context.CommentResponse,
            cancellationToken).ConfigureAwait(false);

        if (WireMapper.TryMapCommentNode(response.CommentView, out CommentNode node))
        {
            return node;
        }

        throw new LemmyApiException($"{Instance.Value} accepted the comment but did not send it back.");
    }

    /// <inheritdoc />
    public async Task<Comment> EditCommentAsync(
        CommentId commentId,
        CommentDraft draft,
        CancellationToken cancellationToken = default)
    {
        RequireSession("edit a comment");
        RequireDraft(draft);

        CommentResponse response = await SendAsync(
            HttpMethod.Put,
            ApiRoot + "comment",
            new EditCommentRequestWire { CommentId = commentId.Value, Content = draft.Value },
            LemmyJson.Context.EditCommentRequestWire,
            LemmyJson.Context.CommentResponse,
            cancellationToken).ConfigureAwait(false);

        return ReadComment(response, "edit");
    }

    /// <inheritdoc />
    public async Task<Comment> SetCommentDeletedAsync(
        CommentId commentId,
        bool deleted,
        CancellationToken cancellationToken = default)
    {
        RequireSession(deleted ? "delete a comment" : "restore a comment");

        CommentResponse response = await PostAsync(
            ApiRoot + "comment/delete",
            new DeleteCommentRequestWire { CommentId = commentId.Value, Deleted = deleted },
            LemmyJson.Context.DeleteCommentRequestWire,
            LemmyJson.Context.CommentResponse,
            cancellationToken).ConfigureAwait(false);

        return ReadComment(response, deleted ? "delete" : "restore");
    }

    private Comment ReadComment(CommentResponse response, string action)
    {
        if (WireMapper.TryMapComment(response.CommentView?.Comment, out Comment comment))
        {
            return comment;
        }

        throw new LemmyApiException($"{Instance.Value} accepted the {action} but did not send the comment back.");
    }

    /// <summary>Catches an empty body here rather than letting the server phrase the complaint.</summary>
    private static void RequireDraft(CommentDraft draft)
    {
        if (!draft.IsValid)
        {
            throw new LemmyApiException("A comment needs something in it.");
        }
    }

    /// <summary>
    /// Fails before the request rather than after it. An unauthenticated write is answered by Lemmy
    /// with a generic error, and "not_logged_in" is not something to put in front of a reader.
    /// </summary>
    private void RequireSession(string action)
    {
        if (!session.IsValid)
        {
            throw new LemmyApiException($"You have to be signed in to {action}.");
        }
    }

    /// <inheritdoc />
    public async Task<PersonProfile> GetPersonAsync(
        PersonId personId,
        CancellationToken cancellationToken = default)
    {
        string endpoint;
        using (var builder = new QueryStringBuilder(stackalloc char[QueryBufferLength]))
        {
            builder.Append("person_id", personId);
            builder.Append("sort", PostSortType.New.ToWire());
            builder.Append("limit", PageSize.Default);
            endpoint = Endpoint("user", builder.Span);
        }

        GetPersonDetailsResponse response = await GetAsync(
            endpoint, LemmyJson.Context.GetPersonDetailsResponse, cancellationToken).ConfigureAwait(false);

        if (WireMapper.TryMapPersonProfile(response, out PersonProfile profile))
        {
            return profile;
        }

        throw new LemmyApiException($"{Instance.Value} did not send an account back.", endpoint, null);
    }

    /// <inheritdoc />
    public async Task<SiteSummary> GetSiteAsync(CancellationToken cancellationToken = default)
    {
        GetSiteResponse response = await GetAsync(ApiRoot + "site", LemmyJson.Context.GetSiteResponse, cancellationToken)
            .ConfigureAwait(false);

        return WireMapper.MapSite(response, Instance);
    }

    private static string Endpoint(string path, ReadOnlySpan<char> query) =>
        string.Concat(ApiRoot.AsSpan(), path.AsSpan(), query);

    /// <summary>Adds the session, when there is one. Anonymous reads simply go without.</summary>
    private void Authorise(HttpRequestMessage request)
    {
        if (session.IsValid)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Value);
        }
    }

    private Task<TResponse> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest body,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TRequest> requestInfo,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResponse> responseInfo,
        CancellationToken cancellationToken)
        where TResponse : class =>
        SendAsync(HttpMethod.Post, endpoint, body, requestInfo, responseInfo, cancellationToken);

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        HttpMethod method,
        string endpoint,
        TRequest body,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TRequest> requestInfo,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResponse> responseInfo,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var request = new HttpRequestMessage(method, new Uri(Instance.BaseUri, endpoint))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, requestInfo),
                System.Text.Encoding.UTF8,
                "application/json"),
        };

        Authorise(request);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new LemmyApiException($"Could not reach {Instance.Value}.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LemmyApiException($"{Instance.Value} took too long to answer.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await DescribeFailureAsync(response, endpoint, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                return await JsonSerializer.DeserializeAsync(content, responseInfo, cancellationToken).ConfigureAwait(false)
                    ?? throw new LemmyApiException($"{Instance.Value} sent an empty body.", endpoint, response.StatusCode);
            }
            catch (JsonException exception)
            {
                throw new LemmyApiException($"{Instance.Value} sent a body this client cannot read.", exception);
            }
        }
    }

    private async Task<TResponse> GetAsync<TResponse>(
        string endpoint,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResponse> typeInfo,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var requestUri = new Uri(Instance.BaseUri, endpoint);

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        Authorise(request);

        HttpResponseMessage response;
        try
        {
            response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new LemmyApiException($"Could not reach {Instance.Value}.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LemmyApiException($"{Instance.Value} took too long to answer.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await DescribeFailureAsync(response, endpoint, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                return await JsonSerializer.DeserializeAsync(body, typeInfo, cancellationToken).ConfigureAwait(false)
                    ?? throw new LemmyApiException($"{Instance.Value} sent an empty body.", endpoint, response.StatusCode);
            }
            catch (JsonException exception)
            {
                throw new LemmyApiException($"{Instance.Value} sent a body this client cannot read.", exception);
            }
        }
    }

    /// <summary>
    /// Turns a failure response into an exception, keeping Lemmy's own error code when it sent one.
    /// The body is best-effort: an instance behind a proxy often answers with HTML instead.
    /// </summary>
    private async Task<LemmyApiException> DescribeFailureAsync(
        HttpResponseMessage response,
        string endpoint,
        CancellationToken cancellationToken)
    {
        string? serverError = null;
        try
        {
            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            ErrorResponse? error = await JsonSerializer
                .DeserializeAsync(body, LemmyJson.Context.ErrorResponse, cancellationToken)
                .ConfigureAwait(false);

            serverError = error?.Error ?? error?.Message;
        }
        catch (JsonException)
        {
            // A non-JSON error body tells us nothing beyond the status code, which we already have.
        }
        catch (HttpRequestException)
        {
            // The connection died mid-body; the status code still stands.
        }

        string detail = serverError is null ? string.Empty : $" ({serverError})";

        return new LemmyApiException(
            $"{Instance.Value} answered {(int)response.StatusCode} {response.ReasonPhrase}{detail}.",
            endpoint,
            response.StatusCode,
            serverError);
    }
}
