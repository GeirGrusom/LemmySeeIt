using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>Changing a post from the page it is on, and starting one from the feed.</summary>
[TestFixture]
internal sealed class PostAmendTests
{
    private TestServices services = null!;
    private RecordingNavigator navigator = null!;

    [SetUp]
    public void SetUp()
    {
        services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        navigator = new RecordingNavigator();
    }

    /// <summary>The account that wrote <see cref="Sample.PostSummary"/>, whose creator is person 1.</summary>
    private void SignInAsTheAuthor() =>
        services.Account.Set(new Account(new PersonId(1), new Username("someone"), null, Sample.Instance, null));

    private void SignInAsSomebodyElse() =>
        services.Account.Set(new Account(new PersonId(99), new Username("stranger"), null, Sample.Instance, null));

    private PostDetailViewModel Page(PostSummary? summary = null) =>
        new(services.Services, navigator, services.Api, summary ?? Sample.PostSummary(), AppSettings.Default);

    private static PostSummary Deleted(PostSummary summary) =>
        summary with { Post = summary.Post with { IsDeleted = true } };

    [Test]
    public void TheAuthorOfAPostMayChangeIt()
    {
        SignInAsTheAuthor();
        using PostDetailViewModel page = Page();

        Assert.Multiple(() =>
        {
            Assert.That(page.IsOwn, Is.True);
            Assert.That(page.CanAmend, Is.True);
            Assert.That(page.CanRestore, Is.False);
        });
    }

    [Test]
    public void SomebodyElsesPostOffersNoEditOrDelete()
    {
        SignInAsSomebodyElse();
        using PostDetailViewModel page = Page();

        Assert.Multiple(() =>
        {
            Assert.That(page.IsOwn, Is.False);
            Assert.That(page.CanAmend, Is.False);
            Assert.That(page.CanRestore, Is.False);
            Assert.That(page.CanComment, Is.True, "anyone signed in can still comment on it");
        });
    }

    [Test]
    public void SignedOutThereIsNothingToDoWithAPost()
    {
        services.Api.IsAuthenticated.Returns(false);
        using PostDetailViewModel page = Page();

        Assert.Multiple(() =>
        {
            Assert.That(page.IsOwn, Is.False);
            Assert.That(page.CanAmend, Is.False);
        });
    }

    [Test]
    public void EditingOpensTheComposerOnThisPost()
    {
        SignInAsTheAuthor();
        using PostDetailViewModel page = Page();

        page.EditPostCommand.Execute(null);

        Assert.That(navigator.Pushed, Has.Count.EqualTo(1));
        var composer = (PostComposerViewModel)navigator.Pushed[0];
        Assert.Multiple(() =>
        {
            Assert.That(composer.IsEditing, Is.True);
            Assert.That(composer.Headline, Is.EqualTo("A post"));
        });
    }

    [Test]
    public async Task DeletingLeavesThePostInPlaceWithAWayBack()
    {
        SignInAsTheAuthor();
        services.Api.SetPostDeletedAsync(Arg.Any<PostId>(), true, Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Deleted(Sample.PostSummary())));

        using PostDetailViewModel page = Page();
        await page.ToggleDeletedCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(page.IsPostDeleted, Is.True);
            Assert.That(page.CanAmend, Is.False);
            Assert.That(page.CanRestore, Is.True, "Lemmy's delete is a flag, so it can be undone");
            Assert.That(page.ActionError, Is.Null);
            Assert.That(page.IsAmending, Is.False);
        });
    }

    [Test]
    public async Task RestoringAskedForTheOppositeOfWhateverThePostIs()
    {
        SignInAsTheAuthor();
        services.Api.SetPostDeletedAsync(Arg.Any<PostId>(), false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Sample.PostSummary()));

        using PostDetailViewModel page = Page(Deleted(Sample.PostSummary()));
        await page.ToggleDeletedCommand.ExecuteAsync(null);

        await services.Api.Received(1).SetPostDeletedAsync(new PostId(10), false, Arg.Any<CancellationToken>());
        Assert.That(page.IsPostDeleted, Is.False);
    }

    [Test]
    public async Task ARefusedDeleteSaysSoAndChangesNothing()
    {
        SignInAsTheAuthor();
        services.Api.SetPostDeletedAsync(Arg.Any<PostId>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task<PostSummary>>(_ => throw new LemmyApiException("That is not yours to delete."));

        using PostDetailViewModel page = Page();
        await page.ToggleDeletedCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(page.ActionError, Is.EqualTo("That is not yours to delete."));
            Assert.That(page.IsPostDeleted, Is.False);
            Assert.That(page.IsAmending, Is.False);
        });
    }

    [Test]
    public async Task AnEditComesBackToThePageAndShowsWhatItNowSays()
    {
        SignInAsTheAuthor();
        PostSummary rewritten = Sample.PostSummary(title: "A better headline") with
        {
            Post = Sample.Post(title: "A better headline") with { Body = new MarkdownText("Rewritten.") },
        };
        services.Api.EditPostAsync(Arg.Any<PostId>(), Arg.Any<PostDraft>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(rewritten));

        using PostDetailViewModel page = Page();
        page.EditPostCommand.Execute(null);

        var composer = (PostComposerViewModel)navigator.Pushed[0];
        composer.Headline = "A better headline";
        await composer.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(navigator.PopCount, Is.EqualTo(1), "the composer is left behind");
            Assert.That(page.Title, Is.EqualTo("A better headline"));
            Assert.That(page.HasBody, Is.True);
            Assert.That(page.Summary.Post.Body.Value, Is.EqualTo("Rewritten."));
        });
    }

    [Test]
    public async Task TheShellHeaderFollowsAPostThatIsRenamed()
    {
        SignInAsTheAuthor();
        services.Api.EditPostAsync(Arg.Any<PostId>(), Arg.Any<PostDraft>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Sample.PostSummary(title: "A better headline")));

        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        var page = new PostDetailViewModel(services.Services, shell, services.Api, Sample.PostSummary(), AppSettings.Default);
        shell.Push(page);

        Assert.That(shell.PageTitle, Is.EqualTo("A post"));

        page.EditPostCommand.Execute(null);
        var composer = (PostComposerViewModel)shell.CurrentPage!;
        composer.Headline = "A better headline";
        await composer.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(shell.CurrentPage, Is.SameAs(page), "saving comes back to the post");
            Assert.That(shell.PageTitle, Is.EqualTo("A better headline"));
        });
    }

    [Test]
    public void TheFeedOffersToPostOnlyWhenThereIsAnAccount()
    {
        using var signedIn = new FeedViewModel(services.Services, navigator, services.Api, AppSettings.Default);
        Assert.That(signedIn.CanPost, Is.True);

        ILemmyApi anonymous = Substitute.For<ILemmyApi>();
        anonymous.IsAuthenticated.Returns(false);
        using var signedOut = new FeedViewModel(services.Services, navigator, anonymous, AppSettings.Default);
        Assert.That(signedOut.CanPost, Is.False);
    }

    [Test]
    public void PostingFromACommunityFeedTakesTheCommunityWithIt()
    {
        using var feed = new FeedViewModel(services.Services, navigator, services.Api, AppSettings.Default, Sample.CommunitySummary());

        feed.NewPostCommand.Execute(null);

        var composer = (PostComposerViewModel)navigator.Pushed.Single();
        Assert.Multiple(() =>
        {
            Assert.That(composer.HasCommunity, Is.True);
            Assert.That(composer.IsChoosingCommunity, Is.False);
        });
    }

    [Test]
    public async Task AFinishedPostGoesToTheTopOfTheFeedAndIsOpened()
    {
        services.Api.CreatePostAsync(Arg.Any<CommunityId>(), Arg.Any<PostDraft>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Sample.PostSummary(id: 42, title: "Brand new")));
        services.FeedReturns(Sample.PostPage(2, null));

        using var feed = new FeedViewModel(services.Services, navigator, services.Api, AppSettings.Default, Sample.CommunitySummary());
        await feed.LoadAsync();

        feed.NewPostCommand.Execute(null);
        var composer = (PostComposerViewModel)navigator.Pushed.Single();
        composer.Headline = "Brand new";
        await composer.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(feed.Posts[0].Summary.Post.Id, Is.EqualTo(new PostId(42)), "a new post does not sort into Hot on its own");
            Assert.That(feed.Posts, Has.Count.EqualTo(3));
            Assert.That(navigator.PopCount, Is.EqualTo(1), "the composer is left behind");
            Assert.That(navigator.Pushed[^1], Is.InstanceOf<PostDetailViewModel>());
        });
    }
}
