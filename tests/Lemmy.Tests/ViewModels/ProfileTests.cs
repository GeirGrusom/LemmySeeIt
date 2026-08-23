using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>An account's own page, and the sign-out that now lives on it.</summary>
[TestFixture]
internal sealed class ProfileTests
{
    private static ProfileViewModel Profile(TestServices services, Func<Task>? signOut = null) =>
        new(
            services.Services,
            new RecordingNavigator(),
            services.Api,
            AppSettings.Default,
            new PersonId(1),
            signOut);

    [Test]
    public async Task ItShowsWhoTheAccountIsAndHowMuchTheyHaveWritten()
    {
        var services = new TestServices();
        using ProfileViewModel page = Profile(services);
        await page.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.Title, Is.EqualTo("alice"));
            Assert.That(page.QualifiedName, Does.StartWith("@alice@"));
            Assert.That(page.ActivityLabel, Is.EqualTo("3 posts · 41 comments"));
            Assert.That(page.Profile, Is.Not.Null);
            Assert.That(page.JoinedLabel, Does.StartWith("joined "));
            Assert.That(page.HasBio, Is.True);
            Assert.That(page.Bio, Is.Not.Empty);
        });
    }

    [Test]
    public async Task ItListsWhatTheyWrote()
    {
        var services = new TestServices();
        using ProfileViewModel page = Profile(services);
        await page.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.Posts, Has.Count.EqualTo(1));
            Assert.That(page.Comments, Has.Count.EqualTo(1));
            Assert.That(page.HasNothing, Is.False);
        });
    }

    [Test]
    public async Task AnAccountThatHasWrittenNothingSaysSo()
    {
        var services = new TestServices();
        services.Api.GetPersonAsync(Arg.Any<PersonId>(), Arg.Any<CancellationToken>())
            .Returns(Sample.PersonProfile() with { Posts = [], Comments = [] });

        using ProfileViewModel page = Profile(services);
        await page.LoadAsync();

        Assert.That(page.HasNothing, Is.True);
    }

    [Test]
    public async Task AFailureIsShownRatherThanLeavingABlankPage()
    {
        var services = new TestServices();
        services.Api.GetPersonAsync(Arg.Any<PersonId>(), Arg.Any<CancellationToken>())
            .Returns<PersonProfile>(_ => throw new LemmyApiException("Could not reach lemmy.world."));

        using ProfileViewModel page = Profile(services);
        await page.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.HasError, Is.True);
            Assert.That(page.ErrorMessage, Is.EqualTo("Could not reach lemmy.world."));
        });
    }

    [Test]
    public async Task SomebodyElsesPageOffersNoWayToSignOut()
    {
        var services = new TestServices();
        using ProfileViewModel page = Profile(services);
        await page.LoadAsync();

        Assert.That(page.CanSignOut, Is.False);
    }

    [Test]
    public async Task TheReadersOwnPageSignsOutWhenAsked()
    {
        var services = new TestServices();
        bool signedOut = false;

        using ProfileViewModel page = Profile(services, () =>
        {
            signedOut = true;
            return Task.CompletedTask;
        });
        await page.LoadAsync();

        Assert.That(page.CanSignOut, Is.True);
        await page.SignOutCommand.ExecuteAsync(null);

        Assert.That(signedOut, Is.True);
    }

    [Test]
    public async Task OpeningACommentGoesToThePostItWasWrittenOn()
    {
        var services = new TestServices();
        services.Api.GetPostAsync(Arg.Any<PostId>(), Arg.Any<CancellationToken>())
            .Returns(Sample.PostSummary());

        var navigator = new RecordingNavigator();
        using var page = new ProfileViewModel(
            services.Services, navigator, services.Api, AppSettings.Default, new PersonId(1));
        await page.LoadAsync();

        await page.Comments[0].OpenCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(navigator.Pushed, Has.Count.EqualTo(1));
            Assert.That(navigator.Pushed[0], Is.TypeOf<PostDetailViewModel>());
        });
    }

    [Test]
    public async Task OnlyPicturesAreOfferedToTheImageViewer()
    {
        var services = new TestServices();
        services.Api.GetPersonAsync(Arg.Any<PersonId>(), Arg.Any<CancellationToken>())
            .Returns(Sample.PersonProfile() with { Posts = [Sample.ImagePostSummary(), Sample.PostSummary()] });

        using ProfileViewModel page = Profile(services);
        await page.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.Images, Has.Length.EqualTo(1));
            Assert.That(page.CanLoadMore, Is.False, "a profile shows one page of what they wrote");
        });
    }

    [Test]
    public async Task ExactlyOneOfSomethingReadsAsOne()
    {
        var services = new TestServices();
        services.Api.GetPersonAsync(Arg.Any<PersonId>(), Arg.Any<CancellationToken>())
            .Returns(Sample.PersonProfile() with
            {
                Tally = new PersonTally(new VoteCount(1), new VoteCount(1)),
            });

        using ProfileViewModel page = Profile(services);
        await page.LoadAsync();

        Assert.That(page.ActivityLabel, Is.EqualTo("1 post · 1 comment"));
    }

    [Test]
    public async Task NoneAndManyBothReadAsPlural()
    {
        var services = new TestServices();
        services.Api.GetPersonAsync(Arg.Any<PersonId>(), Arg.Any<CancellationToken>())
            .Returns(Sample.PersonProfile() with
            {
                Tally = new PersonTally(new VoteCount(0), new VoteCount(1200)),
            });

        using ProfileViewModel page = Profile(services);
        await page.LoadAsync();

        Assert.That(page.ActivityLabel, Is.EqualTo("0 posts · 1.2k comments"));
    }
}
