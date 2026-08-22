using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// The comment box. The behaviour that matters is what happens when posting fails: whatever was
/// typed has to still be there.
/// </summary>
[TestFixture]
internal sealed class CommentComposerViewModelTests
{
    private static CommentComposerViewModel Composer(
        Func<CommentDraft, CancellationToken, Task> submit,
        ComposerPurpose purpose = ComposerPurpose.Comment,
        Action? closed = null,
        string? initialText = null) =>
        new(purpose, submit, closed, initialText);

    [Test]
    public void AnEmptyBoxCannotBePosted()
    {
        CommentComposerViewModel composer = Composer((_, _) => Task.CompletedTask);

        Assert.Multiple(() =>
        {
            Assert.That(composer.SubmitCommand.CanExecute(null), Is.False);
            composer.Text = "   ";
            Assert.That(composer.SubmitCommand.CanExecute(null), Is.False);
            composer.Text = "Something";
            Assert.That(composer.SubmitCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task PostingClearsTheBoxAndClosesIt()
    {
        bool closed = false;
        CommentComposerViewModel composer = Composer((_, _) => Task.CompletedTask, closed: () => closed = true);
        composer.Text = "Posted";

        await composer.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(composer.Text, Is.Empty);
            Assert.That(closed, Is.True);
        });
    }

    [Test]
    public async Task AFailedPostKeepsWhatWasTyped()
    {
        bool closed = false;
        CommentComposerViewModel composer = Composer(
            (_, _) => throw new LemmyApiException("This post is locked."),
            closed: () => closed = true);
        composer.Text = "A long and carefully written comment";

        await composer.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(composer.Text, Is.EqualTo("A long and carefully written comment"));
            Assert.That(composer.ErrorMessage, Is.EqualTo("This post is locked."));
            Assert.That(closed, Is.False, "the box must stay open so the text is not lost");
        });
    }

    [Test]
    public async Task WhatIsPostedIsTheTrimmedDraft()
    {
        CommentDraft? sent = null;
        CommentComposerViewModel composer = Composer((draft, _) =>
        {
            sent = draft;
            return Task.CompletedTask;
        });
        composer.Text = "  padded  ";

        await composer.SubmitCommand.ExecuteAsync(null);

        Assert.That(sent?.Value, Is.EqualTo("padded"));
    }

    [Test]
    public void AnEditBoxStartsWithTheExistingText()
    {
        CommentComposerViewModel composer = Composer(
            (_, _) => Task.CompletedTask,
            ComposerPurpose.Edit,
            initialText: "What it says now");

        Assert.Multiple(() =>
        {
            Assert.That(composer.Text, Is.EqualTo("What it says now"));
            Assert.That(composer.SubmitLabel, Is.EqualTo("Save"));
        });
    }

    [TestCase(ComposerPurpose.Comment, "Post")]
    [TestCase(ComposerPurpose.Reply, "Reply")]
    [TestCase(ComposerPurpose.Edit, "Save")]
    public void TheButtonSaysWhatItWillDo(ComposerPurpose purpose, string expected) =>
        Assert.That(Composer((_, _) => Task.CompletedTask, purpose).SubmitLabel, Is.EqualTo(expected));

    [Test]
    public void ABoxWithNowhereToGoCannotBeDismissed() =>
        Assert.That(Composer((_, _) => Task.CompletedTask).CanDismiss, Is.False);

    [Test]
    public void TheLengthCounterOnlyAppearsWhenItMatters()
    {
        CommentComposerViewModel composer = Composer((_, _) => Task.CompletedTask);

        Assert.Multiple(() =>
        {
            composer.Text = "short";
            Assert.That(composer.IsOverLength, Is.False);

            composer.Text = new string('x', CommentDraft.MaxLength - 100);
            Assert.That(composer.IsOverLength, Is.True);
            Assert.That(composer.LengthLabel, Does.Contain("10000"));
        });
    }

    [Test]
    public async Task AnOverLongCommentIsRefusedWithoutBeingSent()
    {
        bool sent = false;
        CommentComposerViewModel composer = Composer((_, _) =>
        {
            sent = true;
            return Task.CompletedTask;
        });
        composer.Text = new string('x', CommentDraft.MaxLength + 1);

        await composer.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(sent, Is.False);
            Assert.That(composer.ErrorMessage, Does.Contain("at most"));
        });
    }
}
