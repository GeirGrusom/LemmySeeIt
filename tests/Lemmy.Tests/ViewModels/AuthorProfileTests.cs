using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>Bylines lead to whoever wrote the thing.</summary>
[TestFixture]
internal sealed class AuthorProfileTests
{
    [Test]
    public async Task AFeedRowsBylineOpensItsAuthor()
    {
        var services = new TestServices();
        services.FeedReturns(Sample.PostPage(1, null));
        var navigator = new RecordingNavigator();

        using var feed = new FeedViewModel(services.Services, navigator, services.Api, AppSettings.Default);
        await feed.LoadAsync();

        feed.Posts[0].OpenAuthorCommand.Execute(null);

        Assert.That(navigator.ProfilesShown, Is.EqualTo(new[] { Sample.PostSummary().Creator.Id }));
    }

    [Test]
    public async Task APostsBylineOpensItsAuthor()
    {
        var services = new TestServices();
        services.Api.GetCommentsAsync(Arg.Any<CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns(CommentThread.Empty);
        var navigator = new RecordingNavigator();

        using var page = new PostDetailViewModel(
            services.Services, navigator, services.Api, Sample.PostSummary(), AppSettings.Default);
        await page.LoadAsync();

        page.OpenAuthorCommand.Execute(null);

        Assert.That(navigator.ProfilesShown, Is.EqualTo(new[] { Sample.PostSummary().Creator.Id }));
    }

    [Test]
    public async Task ACommentsBylineOpensItsAuthor()
    {
        var services = new TestServices();
        services.Api.GetCommentsAsync(Arg.Any<CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns(new CommentThread([Sample.CommentNode()]));
        var navigator = new RecordingNavigator();

        using var page = new PostDetailViewModel(
            services.Services, navigator, services.Api, Sample.PostSummary(), AppSettings.Default);
        await page.LoadAsync();

        page.Comments[0].OpenAuthorCommand.Execute(null);

        Assert.That(navigator.ProfilesShown, Is.EqualTo(new[] { Sample.CommentNode().Creator.Id }));
    }

    [Test]
    public async Task AReplysBylineOpensItsAuthorToo()
    {
        var services = new TestServices();
        services.Api.GetCommentsAsync(Arg.Any<CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns(new CommentThread(
            [
                Sample.CommentNode(100, "0.100", 1, Sample.CommentNode(200, "0.100.200")),
            ]));
        var navigator = new RecordingNavigator();

        using var page = new PostDetailViewModel(
            services.Services, navigator, services.Api, Sample.PostSummary(), AppSettings.Default);
        await page.LoadAsync();

        page.Comments[0].Replies[0].OpenAuthorCommand.Execute(null);

        Assert.That(navigator.ProfilesShown, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Whose page it is decides whether it offers to sign out, not whichever byline was tapped.
    /// </summary>
    [Test]
    public async Task SomebodyElsesPageDoesNotOfferToSignOutTheReader()
    {
        var services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        services.Api.GetMyAccountAsync(Arg.Any<CancellationToken>())
            .Returns(new Account(new PersonId(1), new Username("alice"), null, Sample.Instance, null));
        await services.SessionStore.SaveAsync(new StoredSession(
            AppSettings.Default.Instance, new SessionToken("jwt.token.value"), new Username("alice")));

        using var shell = new MainViewModel(services.Services, AppSettings.Default);
        await shell.InitialiseAsync();

        shell.ShowProfile(new PersonId(99));
        var theirs = (ProfileViewModel)shell.CurrentPage!;

        shell.ShowProfile(new PersonId(1));
        var mine = (ProfileViewModel)shell.CurrentPage!;

        Assert.Multiple(() =>
        {
            Assert.That(theirs.CanSignOut, Is.False, "somebody else's page");
            Assert.That(mine.CanSignOut, Is.True, "the reader's own");
        });
    }

    [Test]
    public void AnUnknownPersonOpensNothing()
    {
        var services = new TestServices();
        using var shell = new MainViewModel(services.Services, AppSettings.Default);

        shell.ShowProfile(default);

        Assert.That(shell.CurrentPage, Is.Not.TypeOf<ProfileViewModel>());
    }
}
