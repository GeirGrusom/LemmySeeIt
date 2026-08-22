using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

[TestFixture]
internal sealed class PostDetailViewModelTests
{
    private TestServices services = null!;
    private RecordingNavigator navigator = null!;

    [SetUp]
    public void CreateServices()
    {
        services = new TestServices();
        navigator = new RecordingNavigator();
        ThreadReturns(CommentThread.Empty);
    }

    private void ThreadReturns(CommentThread thread) =>
        services.Api.GetCommentsAsync(Arg.Any<CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(thread));

    private PostDetailViewModel CreatePage(PostSummary? summary = null, AppSettings? settings = null) =>
        new(services.Services, navigator, services.Api, summary ?? Sample.PostSummary(), settings ?? AppSettings.Default);

    [Test]
    public async Task LoadAsync_FetchesTheThreadForThatPost()
    {
        using PostDetailViewModel page = CreatePage();

        await page.LoadAsync();

        await services.Api.Received(1).GetCommentsAsync(
            Arg.Is<CommentQuery>(query => query.Post == new PostId(10)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task LoadAsync_BuildsAViewModelPerRootComment()
    {
        ThreadReturns(new CommentThread(
        [
            Sample.CommentNode(100, "0.100", 1, Sample.CommentNode(200, "0.100.200")),
            Sample.CommentNode(300, "0.300"),
        ]));
        using PostDetailViewModel page = CreatePage();

        await page.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.Comments, Has.Count.EqualTo(2));
            Assert.That(page.Comments[0].Replies, Has.Count.EqualTo(1));
            Assert.That(page.HasNoComments, Is.False);
        });
    }

    [Test]
    public async Task LoadAsync_SaysSoWhenThereAreNoComments()
    {
        using PostDetailViewModel page = CreatePage();

        await page.LoadAsync();

        Assert.That(page.HasNoComments, Is.True);
    }

    [Test]
    public async Task ChangingTheCommentSort_RefetchesTheThread()
    {
        using PostDetailViewModel page = CreatePage();
        await page.LoadAsync();

        page.SelectedSort = CommentSortType.New;

        await services.Api.Received(1).GetCommentsAsync(
            Arg.Is<CommentQuery>(query => query.Sort == CommentSortType.New),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void TheStartingCommentSortIsTheOneTheReaderChose()
    {
        using PostDetailViewModel page = CreatePage(settings: AppSettings.Default with { CommentSort = CommentSortType.Top });

        Assert.That(page.SelectedSort, Is.EqualTo(CommentSortType.Top));
    }

    [Test]
    public async Task AFailedThread_ShowsTheServersMessageWithoutLosingThePost()
    {
        services.Api.GetCommentsAsync(Arg.Any<CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<CommentThread>>(_ => throw new LemmyApiException("lemmy.world answered 500."));
        using PostDetailViewModel page = CreatePage();

        await page.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.HasError, Is.True);
            Assert.That(page.Title, Is.EqualTo("A post"), "the post itself came from the feed, not the failed call");
        });
    }

    [Test]
    public void OpeningTheCommunity_NavigatesToItsFeed()
    {
        using PostDetailViewModel page = CreatePage();

        page.OpenCommunityCommand.Execute(null);

        Assert.That(navigator.Pushed.Single(), Is.TypeOf<FeedViewModel>());
    }

    [Test]
    public void ANsfwPost_StartsWithItsImageCoveredWhenTheReaderAskedForThat()
    {
        using PostDetailViewModel page = CreatePage(Sample.PostSummary(isNsfw: true));

        Assert.That(page.IsImageHidden, Is.True);

        page.RevealImageCommand.Execute(null);

        Assert.That(page.IsImageHidden, Is.False);
    }

    [Test]
    public void ANsfwPost_IsNotCoveredWhenTheReaderTurnedBlurringOff()
    {
        using PostDetailViewModel page = CreatePage(
            Sample.PostSummary(isNsfw: true),
            AppSettings.Default with { BlurNsfwImages = false });

        Assert.That(page.IsImageHidden, Is.False);
    }
}
