using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Api;
using Lemmy.Domain;
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
    private readonly ILemmyApi api;
    private readonly CurrentAccount account;
    private readonly MarkdownMedia media;
    private readonly ITextCopier copier;
    private readonly INavigator navigator;
    private readonly DateTimeOffset now;
    private readonly ICommand? linkCommand;

    /// <summary>Wraps a comment for display.</summary>
    /// <param name="node">The comment and its replies.</param>
    /// <param name="now">The current time, for the age label.</param>
    /// <param name="api">The client this comment and its replies vote through.</param>
    /// <param name="media">How pictures written into the comment are fetched and opened.</param>
    /// <param name="navigator">Where the author's name leads.</param>
    /// <param name="linkCommand">
    /// Opens a link pressed inside the comment. Passed down the tree rather than resolved per
    /// comment: a thread is hundreds of these, and they all open links the same way.
    /// </param>
    public CommentViewModel(
        CommentNode node,
        DateTimeOffset now,
        ILemmyApi api,
        CurrentAccount account,
        MarkdownMedia media,
        ITextCopier copier,
        INavigator navigator,
        ICommand? linkCommand = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(copier);
        ArgumentNullException.ThrowIfNull(navigator);

        this.api = api;
        this.account = account;
        this.media = media;
        this.copier = copier;
        this.navigator = navigator;
        this.now = now;
        this.linkCommand = linkCommand;

        Media = media;
        Node = node;
        LinkCommand = linkCommand;
        AgeLabel = RelativeTime.Format(node.Comment.Published, now);
        Content = MarkdownParser.Parse(node.Comment.VisibleContent);
        Votes = new VoteBarViewModel(
            new VoteOutcome(node.MyVote, node.Tally.Score, node.Tally.Upvotes, node.Tally.Downvotes),
            api.IsAuthenticated,
            (vote, token) => api.VoteOnCommentAsync(node.Comment.Id, vote, token));

        Replies = new ObservableCollection<CommentViewModel>(
            node.Replies.Select(reply => new CommentViewModel(reply, now, api, account, media, copier, navigator, linkCommand)));
    }

    /// <summary>The comment and its replies.</summary>
    public CommentNode Node { get; private set; }

    /// <summary>The direct replies, already wrapped.</summary>
    public ObservableCollection<CommentViewModel> Replies { get; }

    /// <summary>
    /// The comment body, parsed into blocks, with removed and deleted comments already substituted
    /// for their placeholder.
    /// </summary>
    public ImmutableArray<MarkdownBlock> Content { get; private set; }

    /// <summary>Opens a link pressed inside this comment.</summary>
    public ICommand? LinkCommand { get; }

    /// <summary>How pictures written into this comment are fetched and opened.</summary>
    public MarkdownMedia Media { get; }

    /// <summary>The author's display name.</summary>
    public string AuthorLabel => Node.Creator.PreferredName;

    /// <summary>Opens the author's page.</summary>
    [RelayCommand]
    private void OpenAuthor() => navigator.ShowProfile(Node.Creator.Id);

    /// <summary>How long ago the comment was made.</summary>
    public string AgeLabel { get; }

    /// <summary>The arrows and the running score.</summary>
    public VoteBarViewModel Votes { get; }

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

    /// <summary>Whether this comment is the reader's own, and so theirs to change.</summary>
    public bool IsOwn => account.Owns(Node.Comment.CreatorId);

    /// <summary>Whether the author has deleted it.</summary>
    public bool IsDeleted => Node.Comment.IsDeleted;

    /// <summary>Whether the reader can edit or delete it: their own, and not already gone.</summary>
    public bool CanAmend => IsOwn && !IsDeleted;

    /// <summary>Whether the reader can put back one of their own they deleted.</summary>
    public bool CanRestore => IsOwn && IsDeleted;

    /// <summary>Whether anybody is signed in to reply at all.</summary>
    public bool CanReply => api.IsAuthenticated && !IsDeleted;

    /// <summary>Whether the comment has been edited since it was posted.</summary>
    public bool WasEdited => Node.Comment.Updated is not null;

    /// <summary>The open reply or edit box, or <see langword="null"/> when neither is open.</summary>
    [ObservableProperty]
    private CommentComposerViewModel? composer;

    /// <summary>Why the last delete or restore did not take, or <see langword="null"/>.</summary>
    [ObservableProperty]
    private string? actionError;

    /// <summary>Set while a delete or restore is in flight.</summary>
    [ObservableProperty]
    private bool isAmending;

    /// <summary>
    /// Copies what the author wrote — the Markdown itself, not the rendered text, because that is
    /// what can be pasted back into a reply and still mean the same thing.
    /// </summary>
    [RelayCommand]
    private async Task CopyAsync() =>
        WasCopied = await copier.CopyAsync(Node.Comment.Content.Value).ConfigureAwait(true);

    /// <summary>Set once a copy succeeds, so the button can say it worked.</summary>
    [ObservableProperty]
    private bool wasCopied;

    /// <summary>Whether there is anything to copy; a deleted comment has nothing.</summary>
    public bool CanCopy => !Node.Comment.Content.IsEmpty && !IsDeleted;

    /// <summary>Opens a box to reply to this comment.</summary>
    [RelayCommand]
    private void Reply() =>
        Composer = new CommentComposerViewModel(
            ComposerPurpose.Reply,
            PostReplyAsync,
            () => Composer = null);

    /// <summary>Opens a box to rewrite this comment, prefilled with what it says.</summary>
    [RelayCommand]
    private void Edit() =>
        Composer = new CommentComposerViewModel(
            ComposerPurpose.Edit,
            SaveEditAsync,
            () => Composer = null,
            Node.Comment.Content.Value);

    /// <summary>Deletes this comment, or puts it back if it is already deleted.</summary>
    [RelayCommand]
    private async Task ToggleDeletedAsync(CancellationToken cancellationToken)
    {
        bool deleted = !IsDeleted;
        IsAmending = true;
        ActionError = null;

        try
        {
            Comment updated = await api
                .SetCommentDeletedAsync(Node.Comment.Id, deleted, cancellationToken)
                .ConfigureAwait(true);

            Adopt(updated);
        }
        catch (OperationCanceledException)
        {
            // The page went away.
        }
        catch (LemmyApiException exception)
        {
            ActionError = exception.Message;
        }
        finally
        {
            IsAmending = false;
        }
    }

    private async Task PostReplyAsync(CommentDraft draft, CancellationToken cancellationToken)
    {
        CommentNode reply = await api
            .CreateCommentAsync(Node.Comment.PostId, Node.Comment.Id, draft, cancellationToken)
            .ConfigureAwait(true);

        // Newest first, and at the top where it can be seen: the thread's sort is the server's
        // opinion of a comment that did not exist when it was asked.
        Replies.Insert(0, new CommentViewModel(reply, now, api, account, media, copier, navigator, linkCommand));

        IsCollapsed = false;
        OnPropertyChanged(nameof(HasReplies));
        OnPropertyChanged(nameof(HasUnloadedReplies));
    }

    private async Task SaveEditAsync(CommentDraft draft, CancellationToken cancellationToken)
    {
        Comment updated = await api
            .EditCommentAsync(Node.Comment.Id, draft, cancellationToken)
            .ConfigureAwait(true);

        Adopt(updated);
    }

    /// <summary>
    /// Takes on a rewritten comment, keeping the replies. The write endpoints answer with the
    /// comment alone, so replacing the whole node would silently drop the thread below it.
    /// </summary>
    private void Adopt(Comment updated)
    {
        Node = Node with { Comment = updated };
        Content = MarkdownParser.Parse(updated.VisibleContent);

        OnPropertyChanged(nameof(Node));
        OnPropertyChanged(nameof(Content));
        OnPropertyChanged(nameof(IsDeleted));
        OnPropertyChanged(nameof(CanAmend));
        OnPropertyChanged(nameof(CanRestore));
        OnPropertyChanged(nameof(CanReply));
        OnPropertyChanged(nameof(WasEdited));
        OnPropertyChanged(nameof(CanCopy));
    }
}
