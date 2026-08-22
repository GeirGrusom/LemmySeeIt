using System.Collections.Immutable;
using System.Net;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Tests.TestSupport;

namespace Lemmy.Tests.Api;

[TestFixture]
internal sealed class LemmyApiClientTests
{
    private static LemmyApiClient CreateClient(StubHttpMessageHandler handler) =>
        new(handler.CreateClient(), Sample.Instance);

    [Test]
    public void Constructor_RejectsADefaultInstance() =>
        Assert.That(
            () => new LemmyApiClient(new HttpClient(), default),
            Throws.TypeOf<ArgumentException>());

    [Test]
    public async Task GetFeedAsync_AsksTheRightUrl()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.PostList);
        LemmyApiClient client = CreateClient(handler);

        await client.GetFeedAsync(new FeedQuery(ListingType.Local, PostSortType.TopWeek, PageSize: PageSize.Clamp(30)));

        Assert.That(
            handler.SingleRequestedUri.ToString(),
            Is.EqualTo("https://lemmy.world/api/v3/post/list?type_=Local&sort=TopWeek&limit=30&show_nsfw=false"));
    }

    /// <summary>
    /// The last link in the chain from the switch to the server. Sending nothing, or sending false,
    /// filters the content out — including for an account whose own settings say to show it.
    /// </summary>
    [Test]
    public async Task GetFeedAsync_AsksForFlaggedContentWhenTheReaderWantsIt()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.PostList);

        await CreateClient(handler).GetFeedAsync(new FeedQuery(ShowNsfw: true));

        Assert.That(handler.SingleRequestedUri.Query, Does.Contain("show_nsfw=true"));
    }

    [Test]
    public async Task GetCommunitiesAsync_AsksForFlaggedCommunitiesWhenTheReaderWantsThem()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.CommunityList);

        await CreateClient(handler).GetCommunitiesAsync(new CommunityQuery(ShowNsfw: true));

        Assert.That(handler.SingleRequestedUri.Query, Does.Contain("show_nsfw=true"));
    }

    [Test]
    public async Task GetFeedAsync_PassesTheCommunityAndCursorWhenThereIsOne()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.PostList);
        LemmyApiClient client = CreateClient(handler);

        var query = new FeedQuery(Community: new CommunityId(79185), Cursor: new PageCursor("P308c595"));
        await client.GetFeedAsync(query);

        Assert.That(handler.SingleRequestedUri.Query, Does.Contain("community_id=79185").And.Contain("page_cursor=P308c595"));
    }

    [Test]
    public async Task GetFeedAsync_MapsThePostAndItsJoinedRows()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.PostList);

        PostPage page = await CreateClient(handler).GetFeedAsync(new FeedQuery());
        PostSummary summary = page.Posts.Single();

        Assert.Multiple(() =>
        {
            Assert.That(summary.Post.Id, Is.EqualTo(new PostId(50908658)));
            Assert.That(summary.Post.Title.Value, Is.EqualTo("This HAS to be satire"));
            Assert.That(summary.Post.Published, Is.EqualTo(new DateTimeOffset(2026, 8, 20, 20, 31, 39, TimeSpan.Zero).AddTicks(1067890)));
            Assert.That(summary.Creator.PreferredName, Is.EqualTo("Canned Tuna"));
            Assert.That(summary.Creator.QualifiedName, Is.EqualTo("@cannedtuna@lemmy.world"));
            Assert.That(summary.Community.QualifiedName, Is.EqualTo("!microblogmemes@lemmy.world"));
            Assert.That(summary.Tally.Score, Is.EqualTo(new Score(718)));
            Assert.That(summary.Tally.Comments, Is.EqualTo(new VoteCount(285)));
            Assert.That(summary.CreatorIsAdmin, Is.True);
            Assert.That(summary.Post.PreviewImage?.Value, Is.EqualTo("https://lemmy.world/pictrs/image/f3440adf.png"));
        });
    }

    [Test]
    public async Task GetFeedAsync_ReadsTheNextPageCursor()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.PostList);

        PostPage page = await CreateClient(handler).GetFeedAsync(new FeedQuery());

        Assert.Multiple(() =>
        {
            Assert.That(page.HasMore, Is.True);
            Assert.That(page.NextCursor?.Value, Is.EqualTo("P308c595"));
        });
    }

    [Test]
    public async Task GetFeedAsync_TreatsAMissingCursorAsTheEndOfTheFeed()
    {
        using var handler = StubHttpMessageHandler.Returning("""{"posts":[]}""");

        PostPage page = await CreateClient(handler).GetFeedAsync(new FeedQuery());

        Assert.Multiple(() =>
        {
            Assert.That(page.Posts, Is.Empty);
            Assert.That(page.HasMore, Is.False);
        });
    }

    /// <summary>
    /// One malformed post must not cost the reader the whole page. Federated content comes from
    /// servers we do not control, and a missing field is a routine event, not an outage.
    /// </summary>
    [Test]
    public async Task GetFeedAsync_SkipsAPostItCannotMapRatherThanFailingThePage()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.PostListWithOneUnmappablePost);

        PostPage page = await CreateClient(handler).GetFeedAsync(new FeedQuery());

        Assert.Multiple(() =>
        {
            Assert.That(page.Posts, Has.Length.EqualTo(1));
            Assert.That(page.Posts[0].Post.Title.Value, Is.EqualTo("Fine"));
        });
    }

    /// <summary>
    /// Older Lemmy builds send timestamps with no offset. Reading those as local time would shift
    /// every age label by the reader's own offset, which is worst for the people furthest from UTC.
    /// </summary>
    [Test]
    public async Task GetFeedAsync_ReadsAnUnlabelledTimestampAsUtc()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.PostListWithUnlabelledTimestamp);

        PostPage page = await CreateClient(handler).GetFeedAsync(new FeedQuery());

        Assert.That(page.Posts[0].Post.Published.Offset, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public async Task GetCommentsAsync_AsksTheRightUrl()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.CommentList);

        await CreateClient(handler).GetCommentsAsync(
            new CommentQuery(new PostId(10), CommentSortType.Top, CommentDepth.Clamp(6), PageSize.Clamp(50)));

        Assert.That(
            handler.SingleRequestedUri.Query,
            Is.EqualTo("?post_id=10&sort=Top&max_depth=6&limit=50&type_=All"));
    }

    [Test]
    public async Task GetCommentsAsync_BuildsTheTreeFromThePaths()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.CommentList);

        CommentThread thread = await CreateClient(handler).GetCommentsAsync(new CommentQuery(new PostId(10)));

        Assert.Multiple(() =>
        {
            Assert.That(thread.Roots, Has.Length.EqualTo(2));
            Assert.That(thread.Roots[0].Id, Is.EqualTo(new CommentId(100)));
            Assert.That(thread.Roots[0].Replies.Single().Id, Is.EqualTo(new CommentId(200)));
            Assert.That(thread.Roots[1].Id, Is.EqualTo(new CommentId(300)));
            Assert.That(thread.Flatten(), Has.Length.EqualTo(3));
        });
    }

    [Test]
    public async Task GetSiteAsync_MapsTheInstanceIdentity()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.Site);

        SiteSummary site = await CreateClient(handler).GetSiteAsync();

        Assert.Multiple(() =>
        {
            Assert.That(site.Name, Is.EqualTo("Lemmy.World"));
            Assert.That(site.SoftwareVersion, Is.EqualTo("0.19.19-9-gc55dd700c"));
            Assert.That(site.Users.ToCompactString(), Is.EqualTo("197k"));
            Assert.That(site.Address, Is.EqualTo(Sample.Instance));
            Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/site"));
        });
    }

    [Test]
    public async Task GetCommunitiesAsync_MapsTheDirectory()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.CommunityList);

        ImmutableArray<CommunitySummary> communities =
            await CreateClient(handler).GetCommunitiesAsync(new CommunityQuery());

        Assert.Multiple(() =>
        {
            Assert.That(communities.Single().Community.QualifiedName, Is.EqualTo("!technology@lemmy.world"));
            Assert.That(communities.Single().Tally.Subscribers.ToCompactString(), Is.EqualTo("87k"));
            Assert.That(handler.SingleRequestedUri.Query, Does.Contain("page=1"));
        });
    }

    [Test]
    public async Task SearchAsync_EncodesTheTermAndReturnsEachKind()
    {
        using var handler = StubHttpMessageHandler.Returning("""{"posts":[],"communities":[],"users":[],"comments":[]}""");
        SearchTerm.TryCreate("linux gaming", out SearchTerm term);

        SearchResults results = await CreateClient(handler).SearchAsync(new SearchQuery(term));

        Assert.Multiple(() =>
        {
            Assert.That(handler.SingleRequestedUri.Query, Does.StartWith("?q=linux+gaming&type_=All"));
            Assert.That(results.IsEmpty, Is.True);
        });
    }

    /// <summary>A term that never passed validation should not become a request at all.</summary>
    [Test]
    public async Task SearchAsync_DoesNotCallTheServerForADefaultTerm()
    {
        using var handler = StubHttpMessageHandler.Returning("{}");

        SearchResults results = await CreateClient(handler).SearchAsync(new SearchQuery(default));

        Assert.Multiple(() =>
        {
            Assert.That(handler.RequestedUris, Is.Empty);
            Assert.That(results, Is.SameAs(SearchResults.Empty));
        });
    }

    [Test]
    public void GetPostAsync_TurnsAFailureStatusIntoAnApiException()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.ErrorBody, HttpStatusCode.NotFound);

        LemmyApiException? exception = Assert.ThrowsAsync<LemmyApiException>(
            async () => await CreateClient(handler).GetPostAsync(new PostId(1)));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(exception.ServerError, Is.EqualTo("couldnt_find_post"));
            Assert.That(exception.Message, Does.Contain("couldnt_find_post"));
            Assert.That(exception.IsTransient, Is.False);
        });
    }

    [Test]
    public void GetFeedAsync_MarksAServerErrorAsWorthRetrying()
    {
        using var handler = StubHttpMessageHandler.Returning("<html>502</html>", HttpStatusCode.BadGateway);

        LemmyApiException? exception = Assert.ThrowsAsync<LemmyApiException>(
            async () => await CreateClient(handler).GetFeedAsync(new FeedQuery()));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.IsTransient, Is.True);
            Assert.That(exception.ServerError, Is.Null, "a non-JSON error body carries nothing beyond the status");
        });
    }

    [Test]
    public void GetFeedAsync_TurnsATransportFailureIntoAnApiException()
    {
        using var handler = StubHttpMessageHandler.Failing(new HttpRequestException("no route to host"));

        LemmyApiException? exception = Assert.ThrowsAsync<LemmyApiException>(
            async () => await CreateClient(handler).GetFeedAsync(new FeedQuery()));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("lemmy.world"));
            Assert.That(exception.InnerException, Is.TypeOf<HttpRequestException>());
            Assert.That(exception.IsTransient, Is.True);
        });
    }

    [Test]
    public void GetFeedAsync_TurnsUnreadableJsonIntoAnApiException()
    {
        using var handler = StubHttpMessageHandler.Returning("{ not json ");

        Assert.ThrowsAsync<LemmyApiException>(async () => await CreateClient(handler).GetFeedAsync(new FeedQuery()));
    }

    [Test]
    public void GetPostAsync_ThrowsWhenTheBodyHasNoUsablePost()
    {
        using var handler = StubHttpMessageHandler.Returning("""{"post_view":null}""");

        Assert.ThrowsAsync<LemmyApiException>(async () => await CreateClient(handler).GetPostAsync(new PostId(1)));
    }

    [Test]
    public void CancellationPropagatesRatherThanBecomingAnApiException()
    {
        using var handler = StubHttpMessageHandler.Returning(WireFixtures.PostList);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<TaskCanceledException>(
            async () => await CreateClient(handler).GetFeedAsync(new FeedQuery(), cancellation.Token));
    }
}
