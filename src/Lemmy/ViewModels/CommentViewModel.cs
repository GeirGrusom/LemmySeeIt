using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Domain.Markdown;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>
/// One comment and its replies. Replies are materialised eagerly because the thread arrives in one
/// response; what is deferred is drawing them, via <see cref="IsCollapsed"/>.
/// </summary>
public sealed partial class CommentViewModel : ViewModelBase
{
    /// <summary>Wraps a comment for display.</summary>
    /// <param name="node">The comment and its replies.</param>
    /// <param name="now">The current time, for the age label.</param>
    /// <param name="linkCommand">
    /// Opens a link pressed inside the comment. Passed down the tree rather than resolved per
    /// comment: a thread is hundreds of these, and they all open links the same way.
    /// </param>
    public CommentViewModel(CommentNode node, DateTimeOffset now, ICommand? linkCommand = null)
    {
        ArgumentNullException.ThrowIfNull(node);

        Node = node;
        LinkCommand = linkCommand;
        AgeLabel = RelativeTime.Format(node.Comment.Published, now);
        Content = MarkdownParser.Parse(node.Comment.VisibleContent);
        Replies = new ObservableCollection<CommentViewModel>(
            node.Replies.Select(reply => new CommentViewModel(reply, now, linkCommand)));
    }

    /// <summary>The comment and its replies.</summary>
    public CommentNode Node { get; }

    /// <summary>The direct replies, already wrapped.</summary>
    public ObservableCollection<CommentViewModel> Replies { get; }

    /// <summary>
    /// The comment body, parsed into blocks, with removed and deleted comments already substituted
    /// for their placeholder.
    /// </summary>
    public ImmutableArray<MarkdownBlock> Content { get; }

    /// <summary>Opens a link pressed inside this comment.</summary>
    public ICommand? LinkCommand { get; }

    /// <summary>The author's display name.</summary>
    public string AuthorLabel => Node.Creator.PreferredName;

    /// <summary>How long ago the comment was made.</summary>
    public string AgeLabel { get; }

    /// <summary>The score, shortened for a badge.</summary>
    public string ScoreLabel => Node.Tally.Score.ToCompactString();

    /// <summary>Whether the author moderates the community or administers the instance.</summary>
    public bool HasAuthorBadge => Node.CreatorIsModerator || Node.CreatorIsAdmin;

    /// <summary>What that badge says.</summary>
    public string AuthorBadgeLabel => Node.CreatorIsAdmin ? "ADMIN" : "MOD";

    /// <summary>Whether the comment has replies to show.</summary>
    public bool HasReplies => Replies.Count > 0;

    /// <summary>Whether the server said there are replies it did not send.</summary>
    public bool HasUnloadedReplies => Node.UnloadedReplyCount > 0;

    /// <summary>How that reads, e.g. <c>12 more replies</c>.</summary>
    public string UnloadedRepliesLabel =>
        Node.UnloadedReplyCount == 1 ? "1 more reply" : $"{Node.UnloadedReplyCount} more replies";

    /// <summary>Whether the replies are folded away.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleLabel))]
    private bool isCollapsed;

    /// <summary>The glyph on the fold control.</summary>
    public string ToggleLabel => IsCollapsed ? "+" : "−";

    /// <summary>Folds or unfolds this comment's replies.</summary>
    [RelayCommand]
    private void ToggleCollapsed() => IsCollapsed = !IsCollapsed;
}
