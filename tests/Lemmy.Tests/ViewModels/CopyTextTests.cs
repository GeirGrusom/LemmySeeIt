using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// Copying text with a button, which on a phone is the only way there is: a finger cannot select.
/// </summary>
[TestFixture]
internal sealed class CopyTextTests
{
    private static CommentViewModel Comment(TestServices services, CommentNode? node = null) =>
        new(
            node ?? Sample.CommentNode(),
            Threads.Context(
                services.Api,
                services.Services.Account,
                services.Copier,
                new MarkdownMedia(services.ImageLoader, null)));

    [Test]
    public async Task CopyingACommentPutsTheMarkdownOnTheClipboard()
    {
        var services = new TestServices();
        CommentViewModel comment = Comment(services);

        await comment.CopyCommand.ExecuteAsync(null);

        // The source, not the rendered text: that is what can be pasted back into a reply.
        await services.Copier.Received(1).CopyAsync("A comment");
        Assert.That(comment.WasCopied, Is.True);
    }

    [Test]
    public async Task AFailedCopyDoesNotClaimItWorked()
    {
        var services = new TestServices();
        services.Copier.CopyAsync(Arg.Any<string?>()).Returns(false);

        CommentViewModel comment = Comment(services);
        await comment.CopyCommand.ExecuteAsync(null);

        Assert.That(comment.WasCopied, Is.False);
    }

    [Test]
    public void ADeletedCommentHasNothingToCopy()
    {
        var services = new TestServices();
        CommentNode deleted = Sample.CommentNode() with
        {
            Comment = Sample.Comment() with { IsDeleted = true },
        };

        Assert.That(Comment(services, deleted).CanCopy, Is.False);
    }

    [Test]
    public async Task CopyingAPostBodyPutsTheMarkdownOnTheClipboard()
    {
        var services = new TestServices();
        services.Api.GetCommentsAsync(Arg.Any<CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns(CommentThread.Empty);

        using var page = new PostDetailViewModel(
            services.Services, new RecordingNavigator(), services.Api, Sample.PostSummary(), AppSettings.Default);

        await page.CopyBodyCommand.ExecuteAsync(null);

        await services.Copier.Received(1).CopyAsync(Sample.PostSummary().Post.Body.Value);
        Assert.That(page.WasCopied, Is.True);
    }

    [Test]
    public void BlankTextIsNotCopiedOverWhateverTheReaderAlreadyHad()
    {
        // The real copier refuses blank text rather than clearing the clipboard.
        var copier = new SystemLinkOpener();

        Assert.Multiple(() =>
        {
            Assert.That(copier.CopyAsync(null).Result, Is.False);
            Assert.That(copier.CopyAsync("   ").Result, Is.False);
        });
    }
}
