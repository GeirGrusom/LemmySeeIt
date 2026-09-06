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
/// One comment and its replies. Replies that arrived with the thread are materialised eagerly, and
/// what is deferred is drawing them, via <see cref="IsCollapsed"/>. Replies the server did not send
/// — a thread runs deeper than one request goes — are fetched on demand by
/// <see cref="LoadMoreRepliesCommand"/>.
/// </summary>
public sealed partial class CommentViewModel : ViewModelBase
{
    private readonly CommentContext context;

    /// <summary>
    /// The comment this one replies to, or <see langword="null"/> at the top of a thread. Needed
    /// because a reply arriving anywhere counts towards the missing-reply total of every comment
    /// above it, and those are the ones already on screen saying what is missing.
    /// </summary>
    private CommentViewModel? parent;

    /// <summary>Whether asking again could bring anything; see <see cref="LoadMoreRepliesAsync"/>.</summary>
    private bool repliesExhausted;

    /// <summary>Wraps a comment for display.</summary>
    /// <param name="node">The comment and the replies that arrived with it.</param>
    /// <param name="context">What every comment in the thread shares.</param>
    public CommentViewModel(CommentNode node, CommentContext context)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        this.context = context;

        Media = context.Media;
        Node = node;
        LinkCommand = context.LinkCommand;
        AgeLabel = RelativeTime.Format(node.Comment.Published, context.Now);
        Content = MarkdownParser.Parse(node.Comment.VisibleContent);
        Votes = new VoteBarViewModel(
            new VoteOutcome(node.MyVote, node.Tally.Score, node.Tally.Upvotes, node.Tally.Downvotes),
            context.Api.IsAuthenticated,
            (vote, token) => context.Api.VoteOnCommentAsync(node.Comment.Id, vote, token));

        Replies = [];
        foreach (CommentNode reply in node.Replies)
        {
            Replies.Add(new CommentViewModel(reply, context) { parent = this });
        }
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
    private void OpenAuthor() => context.Navigator.ShowProfile(Node.Creator.Id);

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

    /// <summary>
    /// Whether there are replies left to fetch. Goes false once a fetch comes back with nothing
    /// new, whatever the count still says.
    /// </summary>
    public bool HasUnloadedReplies => !repliesExhausted && Node.UnloadedReplyCount > 0;

    /// <summary>How that reads on the control that fetches them, e.g. <c>Show 12 more replies</c>.</summary>
    public string UnloadedRepliesLabel => IsLoadingReplies
        ? "Loading replies…"
        : Node.UnloadedReplyCount == 1 ? "Show 1 more reply" : $"Show {Node.UnloadedReplyCount} more replies";

    /// <summary>Set while the missing replies are being fetched.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnloadedRepliesLabel))]
    private bool isLoadingReplies;

    /// <summary>Why the last attempt to fetch the missing replies failed, or <see langword="null"/>.</summary>
    [ObservableProperty]
    private string? repliesError;

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
    public bool IsOwn => context.Account.Owns(Node.Comment.CreatorId);

    /// <summary>Whether the author has deleted it.</summary>
    public bool IsDeleted => Node.Comment.IsDeleted;

    /// <summary>Whether the reader can edit or delete it: their own, and not already gone.</summary>
    public bool CanAmend => IsOwn && !IsDeleted;

    /// <summary>Whether the reader can put back one of their own they deleted.</summary>
    public bool CanRestore => IsOwn && IsDeleted;

    /// <summary>Whether anybody is signed in to reply at all.</summary>
    public bool CanReply => context.Api.IsAuthenticated && !IsDeleted;

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
        WasCopied = await context.Copier.CopyAsync(Node.Comment.Content.Value).ConfigureAwait(true);

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

    /// <summary>
    /// Fetches the replies the server said exist but did not send. Lemmy answers a whole thread in
    /// one request but only down to a fixed depth, so a long back-and-forth is cut off partway and
    /// has to be asked for again from the comment it was cut at.
    /// </summary>
    [RelayCommand]
    private async Task LoadMoreRepliesAsync(CancellationToken cancellationToken)
    {
        IsLoadingReplies = true;
        RepliesError = null;

        try
        {
            // Depth counts from this comment rather than from the post, so this reaches exactly as
            // far below here as the first request reached below the post. The thread's own sort is
            // carried along so what arrives is ordered like what is already on screen.
            var query = new CommentQuery(
                Node.Comment.PostId,
                context.Sort,
                CommentDepth.Default,
                PageSize.Clamp(PageSize.Maximum),
                Node.Comment.Id);

            CommentThread fetched = await context.Api
                .GetCommentsAsync(query, cancellationToken)
                .ConfigureAwait(true);

            // The response leads with this comment itself, which we already have; only what is
            // below it is new.
            int added = Absorb(fetched.Flatten());

            // A count that will not come down means the rest is unreachable: the server counts
            // replies it will not serve — ones a moderator removed, ones from a blocked account.
            // Offering to fetch them a second time would only fail the same way.
            repliesExhausted = added == 0;

            if (added > 0)
            {
                IsCollapsed = false;
            }

            // From the top of the thread rather than from here: a reply filed below this comment
            // is also one of the replies every comment above it was still counting as missing.
            Root().Resync();
        }
        catch (OperationCanceledException)
        {
            // The page went away.
        }
        catch (LemmyApiException exception)
        {
            RepliesError = exception.Message;
        }
        finally
        {
            IsLoadingReplies = false;
        }
    }

    /// <summary>Deletes this comment, or puts it back if it is already deleted.</summary>
    [RelayCommand]
    private async Task ToggleDeletedAsync(CancellationToken cancellationToken)
    {
        bool deleted = !IsDeleted;
        IsAmending = true;
        ActionError = null;

        try
        {
            Comment updated = await context.Api
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

    /// <summary>
    /// Files each fetched comment under the one it replies to, wherever in this sub-thread that is,
    /// and answers how many were new. Placement goes by <see cref="CommentPath"/> rather than by the
    /// shape of the response, because a comment can arrive before the parent it belongs under.
    /// </summary>
    private int Absorb(ImmutableArray<CommentNode> arrivals)
    {
        var hosts = new Dictionary<CommentId, CommentViewModel>();
        Index(hosts);

        var pending = new List<CommentNode>(arrivals.Length);
        foreach (CommentNode arrival in arrivals)
        {
            // Already on screen: a fetch always returns the comment it was asked about, and a
            // repeat fetch returns replies we placed the first time.
            if (!hosts.ContainsKey(arrival.Id))
            {
                pending.Add(arrival);
            }
        }

        int added = 0;
        while (pending.Count > 0)
        {
            var deferred = new List<CommentNode>();

            foreach (CommentNode arrival in pending)
            {
                if (arrival.Comment.Path.ParentId is not { } parentId
                    || !hosts.TryGetValue(parentId, out CommentViewModel? host))
                {
                    deferred.Add(arrival);
                    continue;
                }

                // Stripped of its own replies: those arrive as entries of their own and are placed
                // under it on this pass or the next, so keeping them here would file them twice.
                var child = new CommentViewModel(arrival with { Replies = [] }, context) { parent = host };
                host.Replies.Add(child);
                hosts[arrival.Id] = child;
                added++;
            }

            // Nothing found a home this pass, and another pass would not either: what is left
            // hangs off a comment that did not arrive.
            if (deferred.Count == pending.Count)
            {
                break;
            }

            pending = deferred;
        }

        return added;
    }

    /// <summary>The comment at the top of this one's thread, which is this one when it is a root.</summary>
    private CommentViewModel Root()
    {
        CommentViewModel top = this;
        while (top.parent is { } above)
        {
            top = above;
        }

        return top;
    }

    /// <summary>Collects this comment and everything below it, so arrivals can be filed under any of them.</summary>
    private void Index(Dictionary<CommentId, CommentViewModel> hosts)
    {
        hosts[Node.Comment.Id] = this;
        foreach (CommentViewModel reply in Replies)
        {
            reply.Index(hosts);
        }
    }

    /// <summary>
    /// Puts the nodes back in step with the view models below them, deepest first, so that the
    /// count of replies still missing is worked out from what is actually on screen.
    /// </summary>
    private void Resync()
    {
        foreach (CommentViewModel reply in Replies)
        {
            reply.Resync();
        }

        Node = Node with { Replies = [.. Replies.Select(reply => reply.Node)] };

        OnPropertyChanged(nameof(Node));
        OnPropertyChanged(nameof(HasReplies));
        OnPropertyChanged(nameof(HasUnloadedReplies));
        OnPropertyChanged(nameof(UnloadedRepliesLabel));
    }

    private async Task PostReplyAsync(CommentDraft draft, CancellationToken cancellationToken)
    {
        CommentNode reply = await context.Api
            .CreateCommentAsync(Node.Comment.PostId, Node.Comment.Id, draft, cancellationToken)
            .ConfigureAwait(true);

        // Newest first, and at the top where it can be seen: the thread's sort is the server's
        // opinion of a comment that did not exist when it was asked.
        Replies.Insert(0, new CommentViewModel(reply, context) { parent = this });

        IsCollapsed = false;
        Root().Resync();
    }

    private async Task SaveEditAsync(CommentDraft draft, CancellationToken cancellationToken)
    {
        Comment updated = await context.Api
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
