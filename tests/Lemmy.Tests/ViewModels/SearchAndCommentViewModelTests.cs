using Lemmy.Api;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

[TestFixture]
internal sealed class SearchViewModelTests
{
    private TestServices services = null!;
    private RecordingNavigator navigator = null!;

    [SetUp]
    public void CreateServices()
    {
        services = new TestServices();
        navigator = new RecordingNavigator();
        services.Api.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SearchResults.Empty));
    }

    private SearchViewModel CreateSearch() =>
        new(services.Services, navigator, services.Api, AppSettings.Default);

    [TestCase("", false)]
    [TestCase("a", false)]
    [TestCase("linux", true)]
    public void CanSearch_FollowsWhetherTheTextIsAUsableTerm(string text, bool expected)
    {
        using SearchViewModel search = CreateSearch();
        search.QueryText = text;

        Assert.Multiple(() =>
        {
            Assert.That(search.CanSearch, Is.EqualTo(expected));
            Assert.That(search.SearchCommand.CanExecute(null), Is.EqualTo(expected));
        });
    }

    [Test]
    public async Task SearchAsync_SplitsTheResultsByKind()
    {
        services.Api.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new SearchResults([Sample.PostSummary()], [Sample.CommunitySummary()], [])));
        using SearchViewModel search = CreateSearch();
        search.QueryText = "linux";

        await search.SearchAsync();

        Assert.Multiple(() =>
        {
            Assert.That(search.Posts, Has.Count.EqualTo(1));
            Assert.That(search.Communities, Has.Count.EqualTo(1));
            Assert.That(search.HasPostResults, Is.True);
            Assert.That(search.HasCommunityResults, Is.True);
            Assert.That(search.FoundNothing, Is.False);
        });
    }

    [Test]
    public async Task SearchAsync_SaysSoWhenNothingMatched()
    {
        using SearchViewModel search = CreateSearch();
        search.QueryText = "zzzzzz";

        await search.SearchAsync();

        Assert.Multiple(() =>
        {
            Assert.That(search.FoundNothing, Is.True);
            Assert.That(search.HasSearched, Is.True);
        });
    }

    [Test]
    public async Task SearchAsync_DiscardsThePreviousResults()
    {
        services.Api.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new SearchResults([Sample.PostSummary()], [], [])),
            Task.FromResult(SearchResults.Empty));
        using SearchViewModel search = CreateSearch();
        search.QueryText = "linux";
        await search.SearchAsync();

        await search.SearchAsync();

        Assert.That(search.Posts, Is.Empty);
    }

    [Test]
    public async Task SearchAsync_DoesNothingForAnUnusableTerm()
    {
        using SearchViewModel search = CreateSearch();
        search.QueryText = "a";

        await search.SearchAsync();

        await services.Api.DidNotReceive().SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void NothingHasBeenSearchedForBeforeTheFirstSearch()
    {
        using SearchViewModel search = CreateSearch();

        Assert.That(search.HasSearched, Is.False);
    }
}

[TestFixture]
internal sealed class CommentViewModelTests
{
    private static readonly DateTimeOffset Now = FixedTimeProvider.Reference;

    [Test]
    public void RepliesAreWrappedAllTheWayDown()
    {
        CommentNode node = Sample.CommentNode(
            1, "0.1", 2,
            Sample.CommentNode(2, "0.1.2", 1, Sample.CommentNode(3, "0.1.2.3")));

        var viewModel = new CommentViewModel(node, Now, AnonymousApi(), new CurrentAccount());

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Replies, Has.Count.EqualTo(1));
            Assert.That(viewModel.Replies[0].Replies, Has.Count.EqualTo(1));
            Assert.That(viewModel.HasReplies, Is.True);
            Assert.That(viewModel.Replies[0].Replies[0].HasReplies, Is.False);
        });
    }

    [Test]
    public void CollapsingFlipsTheGlyphAsWellAsTheState()
    {
        var viewModel = new CommentViewModel(Sample.CommentNode(), Now, AnonymousApi(), new CurrentAccount());

        Assert.That(viewModel.ToggleLabel, Is.EqualTo("−"));

        viewModel.ToggleCollapsedCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsCollapsed, Is.True);
            Assert.That(viewModel.ToggleLabel, Is.EqualTo("+"));
        });
    }

    [TestCase(1, "1 more reply")]
    [TestCase(12, "12 more replies")]
    public void UnloadedRepliesReadNaturally(int childCount, string expected)
    {
        var viewModel = new CommentViewModel(Sample.CommentNode(1, "0.1", childCount), Now, AnonymousApi(), new CurrentAccount());

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasUnloadedReplies, Is.True);
            Assert.That(viewModel.UnloadedRepliesLabel, Is.EqualTo(expected));
        });
    }

    [Test]
    public void ARemovedCommentShowsAPlaceholderRatherThanItsOriginalText()
    {
        Comment removed = Sample.Comment(content: "the original text") with { IsRemoved = true };

        Assert.That(removed.VisibleContent.Value, Is.EqualTo("*Removed by moderator*"));
    }

    [Test]
    public void ADeletedCommentShowsAPlaceholderToo()
    {
        Comment deleted = Sample.Comment(content: "the original text") with { IsDeleted = true };

        Assert.That(deleted.VisibleContent.Value, Is.EqualTo("*Deleted by author*"));
    }

    /// <summary>A client with no session, which is what a comment gets when nobody is signed in.</summary>
    private static ILemmyApi AnonymousApi()
    {
        ILemmyApi api = Substitute.For<ILemmyApi>();
        api.IsAuthenticated.Returns(false);
        return api;
    }
}
