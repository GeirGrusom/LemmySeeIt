using System.Collections.Immutable;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Domain;
using Lemmy.Domain.Markdown;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>
/// One comment on an account's page.
/// </summary>
/// <remarks>
/// Deliberately not <see cref="CommentViewModel"/>. That models a comment inside its thread — with
/// replies under it, a box to answer it and the author's own controls — and none of that applies to
/// a list of things somebody wrote, taken out of the threads they were written in. What this offers
/// instead is the way back: opening the post it belongs to.
/// </remarks>
public sealed partial class ProfileCommentViewModel : ViewModelBase
{
    private readonly Func<PostId, Task> openPost;

    /// <summary>Wraps a comment for an account's page.</summary>
    public ProfileCommentViewModel(CommentNode node, DateTimeOffset now, Func<PostId, Task> openPost)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(openPost);

        Node = node;
        this.openPost = openPost;

        AgeLabel = RelativeTime.Format(node.Comment.Published, now);
        Content = MarkdownParser.Parse(node.Comment.VisibleContent);
    }

    /// <summary>The comment.</summary>
    public CommentNode Node { get; }

    /// <summary>The body, parsed into blocks.</summary>
    public ImmutableArray<MarkdownBlock> Content { get; }

    /// <summary>How long ago it was written.</summary>
    public string AgeLabel { get; }

    /// <summary>Its score, shortened for a badge.</summary>
    public string ScoreLabel => Node.Tally.Score.ToCompactString();

    /// <summary>Opens the post this comment was written on.</summary>
    [RelayCommand]
    private Task Open() => openPost(Node.Comment.PostId);
}
