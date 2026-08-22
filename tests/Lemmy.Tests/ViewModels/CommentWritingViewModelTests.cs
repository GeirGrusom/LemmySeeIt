using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>Writing, rewriting and deleting a comment from the thread it sits in.</summary>
[TestFixture]
internal sealed class CommentWritingViewModelTests
{
    private static readonly DateTimeOffset Now = FixedTimeProvider.Reference;

    /// <summary>An account that wrote <see cref="Sample.Comment"/>, whose creator is person 1.</summary>
    private static CurrentAccount TheAuthor()
    {
        var account = new CurrentAccount();
        account.Set(new Account(
            new PersonId(1),
            new Username("someone"),
            null,
            Sample.Instance,
            null));
        return account;
    }

    private static CurrentAccount SomebodyElse()
    {
        var account = new CurrentAccount();
        account.Set(new Account(
            new PersonId(99),
            new Username("stranger"),
            null,
            Sample.Instance,
            null));
        return account;
    }

    private static ILemmyApi SignedInApi()
    {
        ILemmyApi api = Substitute.For<ILemmyApi>();
        api.IsAuthenticated.Returns(true);
        return api;
    }

    [Test]
    public void TheAuthorOfACommentMayChangeIt()
    {
        var comment = new CommentViewModel(Sample.CommentNode(), Now, SignedInApi(), TheAuthor());

        Assert.Multiple(() =>
        {
            Assert.That(comment.IsOwn, Is.True);
            Assert.That(comment.CanAmend, Is.True);
            Assert.That(comment.CanRestore, Is.False);
        });
    }

    [Test]
    public void SomebodyElsesCommentOffersNoEditOrDelete()
    {
        var comment = new CommentViewModel(Sample.CommentNode(), Now, SignedInApi(), SomebodyElse());

        Assert.Multiple(() =>
        {
            Assert.That(comment.IsOwn, Is.False);
            Assert.That(comment.CanAmend, Is.False);
            Assert.That(comment.CanReply, Is.True, "anyone signed in can still reply");
        });
    }

    [Test]
    public void SignedOutThereIsNothingToDoWithAComment()
    {
        ILemmyApi api = Substitute.For<ILemmyApi>();
        api.IsAuthenticated.Returns(false);

        var comment = new CommentViewModel(Sample.CommentNode(), Now, api, new CurrentAccount());

        Assert.Multiple(() =>
        {
            Assert.That(comment.CanReply, Is.False);
            Assert.That(comment.CanAmend, Is.False);
        });
    }

    [Test]
    public async Task AReplyAppearsUnderTheCommentItAnswers()
    {
        ILemmyApi api = SignedInApi();
        api.CreateCommentAsync(Arg.Any<PostId>(), Arg.Any<CommentId?>(), Arg.Any<CommentDraft>(), Arg.Any<CancellationToken>())
            .Returns(Sample.CommentNode(id: 201, path: "0.100.201"));

        var comment = new CommentViewModel(Sample.CommentNode(), Now, api, TheAuthor());
        comment.ReplyCommand.Execute(null);
        comment.Composer!.Text = "Quite so.";
        await comment.Composer.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(comment.Replies, Has.Count.EqualTo(1));
            Assert.That(comment.Replies[0].Node.Comment.Id, Is.EqualTo(new CommentId(201)));
            Assert.That(comment.Composer, Is.Null, "the box closes once the reply is posted");
        });
    }

    [Test]
    public async Task AReplyIsSentAgainstTheCommentBeingAnswered()
    {
        ILemmyApi api = SignedInApi();
        api.CreateCommentAsync(Arg.Any<PostId>(), Arg.Any<CommentId?>(), Arg.Any<CommentDraft>(), Arg.Any<CancellationToken>())
            .Returns(Sample.CommentNode(id: 201));

        var comment = new CommentViewModel(Sample.CommentNode(id: 100), Now, api, TheAuthor());
        comment.ReplyCommand.Execute(null);
        comment.Composer!.Text = "Quite so.";
        await comment.Composer.SubmitCommand.ExecuteAsync(null);

        await api.Received(1).CreateCommentAsync(
            new PostId(10),
            new CommentId(100),
            Arg.Is<CommentDraft>(draft => draft.Value == "Quite so."),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The edit endpoint answers with the comment alone. Taking the whole node from it would throw
    /// away the thread hanging below the comment being edited.
    /// </summary>
    [Test]
    public async Task EditingRewritesTheTextAndKeepsTheRepliesBelowIt()
    {
        ILemmyApi api = SignedInApi();
        Comment rewritten = Sample.Comment(content: "Rewritten, with feeling") with { Updated = Now };
        api.EditCommentAsync(Arg.Any<CommentId>(), Arg.Any<CommentDraft>(), Arg.Any<CancellationToken>())
            .Returns(rewritten);

        CommentNode node = Sample.CommentNode(replies: Sample.CommentNode(id: 300, path: "0.100.300"));
        var comment = new CommentViewModel(node, Now, api, TheAuthor());

        comment.EditCommand.Execute(null);
        Assert.That(comment.Composer!.Text, Is.EqualTo("A comment"), "the box starts with what is there");

        comment.Composer.Text = "Rewritten, with feeling";
        await comment.Composer.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(comment.Node.Comment.Content.Value, Is.EqualTo("Rewritten, with feeling"));
            Assert.That(comment.Replies, Has.Count.EqualTo(1), "the reply must survive the edit");
            Assert.That(comment.Node.Replies, Has.Length.EqualTo(1));
            Assert.That(comment.WasEdited, Is.True);
            Assert.That(comment.Composer, Is.Null);
        });
    }

    [Test]
    public async Task DeletingLeavesThePlaceholderAndOffersToPutItBack()
    {
        ILemmyApi api = SignedInApi();
        api.SetCommentDeletedAsync(Arg.Any<CommentId>(), true, Arg.Any<CancellationToken>())
            .Returns(Sample.Comment() with { IsDeleted = true });

        var comment = new CommentViewModel(Sample.CommentNode(), Now, api, TheAuthor());
        await comment.ToggleDeletedCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(comment.IsDeleted, Is.True);
            Assert.That(comment.CanAmend, Is.False);
            Assert.That(comment.CanRestore, Is.True);
            Assert.That(comment.Node.Comment.VisibleContent.Value, Does.Contain("Deleted by author"));
        });
    }

    [Test]
    public async Task RestoringPutsTheTextBack()
    {
        ILemmyApi api = SignedInApi();
        api.SetCommentDeletedAsync(Arg.Any<CommentId>(), false, Arg.Any<CancellationToken>())
            .Returns(Sample.Comment());

        CommentNode deleted = Sample.CommentNode() with
        {
            Comment = Sample.Comment() with { IsDeleted = true },
        };
        var comment = new CommentViewModel(deleted, Now, api, TheAuthor());

        Assert.That(comment.CanRestore, Is.True);
        await comment.ToggleDeletedCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(comment.IsDeleted, Is.False);
            Assert.That(comment.CanAmend, Is.True);
        });
    }

    [Test]
    public async Task AFailedDeleteSaysWhyAndChangesNothing()
    {
        ILemmyApi api = SignedInApi();
        api.SetCommentDeletedAsync(Arg.Any<CommentId>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Comment>(_ => throw new LemmyApiException("Could not reach lemmy.world."));

        var comment = new CommentViewModel(Sample.CommentNode(), Now, api, TheAuthor());
        await comment.ToggleDeletedCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(comment.IsDeleted, Is.False);
            Assert.That(comment.ActionError, Is.EqualTo("Could not reach lemmy.world."));
        });
    }
}
