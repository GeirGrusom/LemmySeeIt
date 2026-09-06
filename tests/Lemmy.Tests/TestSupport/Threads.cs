using System.Collections.Immutable;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.ViewModels;

namespace Lemmy.Tests.TestSupport;

/// <summary>
/// Builders for the things a comment view model is wrapped in. Every argument has a default, so a
/// test names only the piece it is actually about.
/// </summary>
internal static class Threads
{
    /// <summary>A client with no session, which is what a comment gets when nobody is signed in.</summary>
    internal static ILemmyApi AnonymousApi()
    {
        ILemmyApi api = Substitute.For<ILemmyApi>();
        api.IsAuthenticated.Returns(false);
        return api;
    }

    /// <summary>A clipboard that always accepts.</summary>
    internal static ITextCopier Copier()
    {
        ITextCopier copier = Substitute.For<ITextCopier>();
        copier.CopyAsync(Arg.Any<string?>()).Returns(true);
        return copier;
    }

    /// <summary>Pictures are irrelevant to most of these; the renderer only needs something to hold.</summary>
    internal static MarkdownMedia Media() => new(Substitute.For<IImageLoader>(), null);

    /// <summary>What every comment in a thread shares.</summary>
    internal static CommentContext Context(
        ILemmyApi? api = null,
        CurrentAccount? account = null,
        ITextCopier? copier = null,
        MarkdownMedia? media = null,
        CommentSortType sort = CommentSortType.Hot) =>
        new(
            api ?? AnonymousApi(),
            account ?? new CurrentAccount(),
            media ?? Media(),
            copier ?? Copier(),
            new RecordingNavigator(),
            FixedTimeProvider.Reference,
            sort);

    /// <summary>
    /// A sub-thread as "show more replies" gets one back: the comment that was asked about, then
    /// the descendants the first request did not reach, flat and in no particular order.
    /// </summary>
    internal static CommentThread SubThread(params CommentNode[] comments) =>
        new([.. comments]);

    /// <summary>A reply built from its path, which is the only thing that says where it belongs.</summary>
    internal static CommentNode Reply(int id, string path, int childCount = 0) =>
        Sample.CommentNode(id, path, childCount);

    /// <summary>Everything on screen below <paramref name="comment"/>, deepest last.</summary>
    internal static ImmutableArray<CommentId> LoadedBelow(CommentViewModel comment)
    {
        var builder = ImmutableArray.CreateBuilder<CommentId>();
        Collect(comment, builder);
        return builder.ToImmutable();
    }

    private static void Collect(CommentViewModel comment, ImmutableArray<CommentId>.Builder builder)
    {
        foreach (CommentViewModel reply in comment.Replies)
        {
            builder.Add(reply.Node.Comment.Id);
            Collect(reply, builder);
        }
    }
}
