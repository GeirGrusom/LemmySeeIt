using System.Collections.Immutable;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>The form for writing a post: what it refuses, what it sends, and where it goes next.</summary>
[TestFixture]
internal sealed class PostComposerViewModelTests
{
    private TestServices services = null!;
    private RecordingNavigator navigator = null!;
    private List<PostSummary> handedOn = null!;

    [SetUp]
    public void SetUp()
    {
        services = new TestServices();
        services.Api.IsAuthenticated.Returns(true);
        services.Api.CreatePostAsync(Arg.Any<CommunityId>(), Arg.Any<PostDraft>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Sample.PostSummary()));
        services.Api.EditPostAsync(Arg.Any<PostId>(), Arg.Any<PostDraft>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Sample.PostSummary()));

        navigator = new RecordingNavigator();
        handedOn = [];
    }

    private PostComposerViewModel NewPost(CommunitySummary? community) =>
        PostComposerViewModel.ForNewPost(services.Services, navigator, services.Api, community, handedOn.Add);

    private PostComposerViewModel Edit(PostSummary post) =>
        PostComposerViewModel.ForEdit(services.Services, navigator, services.Api, post, handedOn.Add);

    [Test]
    public void ComingFromACommunityTheFormOpensOnTheForm()
    {
        using PostComposerViewModel composer = NewPost(Sample.CommunitySummary());

        Assert.Multiple(() =>
        {
            Assert.That(composer.IsChoosingCommunity, Is.False);
            Assert.That(composer.HasCommunity, Is.True);
            Assert.That(composer.CommunityLabel, Does.Contain("technology"));
            Assert.That(composer.CanChooseCommunity, Is.True, "a new post can still go somewhere else");
        });
    }

    [Test]
    public async Task ComingFromTheFrontPageTheCommunityIsAskedForFirst()
    {
        services.Api.GetCommunitiesAsync(Arg.Any<CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ImmutableArray.Create(Sample.CommunitySummary())));

        using PostComposerViewModel composer = NewPost(null);
        await composer.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(composer.IsChoosingCommunity, Is.True);
            Assert.That(composer.Choices, Has.Count.EqualTo(1));
            Assert.That(composer.SubmitCommand.CanExecute(null), Is.False, "there is nowhere to post it yet");
        });
    }

    [Test]
    public async Task WithNothingTypedThePickerOffersWhatTheReaderFollows()
    {
        using PostComposerViewModel composer = NewPost(null);
        await composer.LoadAsync();

        await services.Api.Received(1).GetCommunitiesAsync(
            Arg.Is<CommunityQuery>(query => query.Listing == ListingType.Subscribed),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TypingSearchesTheWiderDirectoryInstead()
    {
        using PostComposerViewModel composer = NewPost(null);
        composer.CommunitySearch = "cooking";

        await composer.FindCommunitiesCommand.ExecuteAsync(null);

        await services.Api.Received(1).SearchAsync(
            Arg.Is<SearchQuery>(query => query.Kind == SearchKind.Communities && query.Term.Value == "cooking"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PickingOneClosesThePickerAndUnblocksTheForm()
    {
        using PostComposerViewModel composer = NewPost(null);
        composer.Headline = "A headline";

        composer.PickCommunityCommand.Execute(Sample.CommunitySummary());

        Assert.Multiple(() =>
        {
            Assert.That(composer.IsChoosingCommunity, Is.False);
            Assert.That(composer.SubmitCommand.CanExecute(null), Is.True);
        });

        await composer.SubmitCommand.ExecuteAsync(null);
        await services.Api.Received(1).CreatePostAsync(new CommunityId(2), Arg.Any<PostDraft>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void AHeadlineIsRequiredBeforeAnythingCanBeSent()
    {
        using PostComposerViewModel composer = NewPost(Sample.CommunitySummary());

        Assert.That(composer.SubmitCommand.CanExecute(null), Is.False);

        composer.Headline = "   ";
        Assert.That(composer.SubmitCommand.CanExecute(null), Is.False, "whitespace is not a headline");

        composer.Headline = "A headline";
        Assert.That(composer.SubmitCommand.CanExecute(null), Is.True);
    }

    [Test]
    public void ABadLinkIsComplainedAboutWhileItIsBeingTyped()
    {
        using PostComposerViewModel composer = NewPost(Sample.CommunitySummary());

        composer.Link = "ftp://example.com";
        Assert.That(composer.HasLinkError, Is.True);

        composer.Link = "example.com";
        Assert.That(composer.HasLinkError, Is.False, "a missing scheme is filled in rather than refused");
    }

    [Test]
    public async Task WhatWasTypedIsWhatIsSent()
    {
        using PostComposerViewModel composer = NewPost(Sample.CommunitySummary());
        composer.Headline = "  A headline  ";
        composer.Link = "example.com/article";
        composer.Body = "Something to say.";
        composer.IsNsfw = true;

        await composer.SubmitCommand.ExecuteAsync(null);

        await services.Api.Received(1).CreatePostAsync(
            new CommunityId(2),
            Arg.Is<PostDraft>(draft =>
                draft.Title.Value == "A headline"
                && draft.Url!.Value.Value == "https://example.com/article"
                && draft.Body.Value == "Something to say."
                && draft.IsNsfw),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TheFinishedPostIsHandedOn()
    {
        using PostComposerViewModel composer = NewPost(Sample.CommunitySummary());
        composer.Headline = "A headline";

        await composer.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(handedOn, Has.Count.EqualTo(1));
            Assert.That(handedOn[0].Post.Id, Is.EqualTo(new PostId(10)));
            Assert.That(composer.IsSending, Is.False);
        });
    }

    [Test]
    public async Task ARefusedPostKeepsEverythingThatWasTyped()
    {
        services.Api.CreatePostAsync(Arg.Any<CommunityId>(), Arg.Any<PostDraft>(), Arg.Any<CancellationToken>())
            .Returns<Task<PostSummary>>(_ => throw new LemmyApiException("You are banned from that community."));

        using PostComposerViewModel composer = NewPost(Sample.CommunitySummary());
        composer.Headline = "A headline";
        composer.Body = "Something to say.";

        await composer.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(composer.ErrorMessage, Is.EqualTo("You are banned from that community."));
            Assert.That(composer.Headline, Is.EqualTo("A headline"), "losing the draft is the one unforgivable outcome");
            Assert.That(composer.Body, Is.EqualTo("Something to say."));
            Assert.That(handedOn, Is.Empty);
            Assert.That(composer.IsSending, Is.False);
        });
    }

    [Test]
    public void EditingOpensOnWhatThePostAlreadySays()
    {
        PostSummary post = Sample.PostSummary(title: "The old headline");
        using PostComposerViewModel composer = Edit(post);

        Assert.Multiple(() =>
        {
            Assert.That(composer.Title, Is.EqualTo("Edit post"));
            Assert.That(composer.Headline, Is.EqualTo("The old headline"));
            Assert.That(composer.SubmitLabel, Is.EqualTo("Save"));
            Assert.That(composer.CanChooseCommunity, Is.False, "Lemmy cannot move a post");
            Assert.That(composer.IsChoosingCommunity, Is.False);
        });
    }

    [Test]
    public async Task AnEditGoesToTheEditEndpointWithThePostsOwnIdentifier()
    {
        using PostComposerViewModel composer = Edit(Sample.PostSummary());
        composer.Headline = "A better headline";

        await composer.SubmitCommand.ExecuteAsync(null);

        await services.Api.Received(1).EditPostAsync(
            new PostId(10),
            Arg.Is<PostDraft>(draft => draft.Title.Value == "A better headline"),
            Arg.Any<CancellationToken>());
        Assert.That(handedOn, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ClearingTheLinkOnAnEditSendsADraftWithoutOne()
    {
        PostSummary link = Sample.PostSummary() with { Post = Sample.Post(url: "https://example.com/article") };
        using PostComposerViewModel composer = Edit(link);

        Assert.That(composer.Link, Is.EqualTo("https://example.com/article"), "the form opens on what the post says");

        composer.Link = string.Empty;

        await composer.SubmitCommand.ExecuteAsync(null);

        await services.Api.Received(1).EditPostAsync(
            Arg.Any<PostId>(),
            Arg.Is<PostDraft>(draft => !draft.HasUrl),
            Arg.Any<CancellationToken>());
    }
}
