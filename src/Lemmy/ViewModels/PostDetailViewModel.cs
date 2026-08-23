using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Markdown;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>
/// A single post with its comment thread. The post itself is handed in from the feed row that was
/// tapped, so the body renders immediately and only the comments are waited on.
/// </summary>
public sealed partial class PostDetailViewModel : PageViewModel
{
    /// <summary>Post images get the full content width, so they are decoded larger than a thumbnail.</summary>
    private const int ImageDecodeWidth = 1080;

    /// <summary>One request deep enough to cover almost every thread, without a wasteful payload.</summary>
    private static readonly CommentDepth ThreadDepth = CommentDepth.Clamp(8);

    private readonly ILemmyApi api;
    private readonly AppSettings settings;

    /// <summary>Opens a post that a feed already loaded.</summary>
    /// <param name="services">The app's services.</param>
    /// <param name="navigator">Where the community link goes.</param>
    /// <param name="api">The instance to read.</param>
    /// <param name="summary">The post, already fetched.</param>
    /// <param name="settings">The reader's saved preferences.</param>
    public PostDetailViewModel(
        AppServices services,
        INavigator navigator,
        ILemmyApi api,
        PostSummary summary,
        AppSettings settings)
        : base(services, navigator)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(settings);

        this.api = api;
        this.settings = settings;
        Summary = summary;
        AgeLabel = RelativeTime.Format(summary.Post.Published, services.Now);
        Body = MarkdownParser.Parse(summary.Post.Body);
        isImageHidden = settings.BlurNsfwImages && summary.Post.IsNsfw;
        selectedSort = settings.CommentSort;

        Subscription = new SubscribeButtonViewModel(
            summary.Community.Id,
            summary.Subscription,
            api.IsAuthenticated,
            services.Subscriptions,
            (follow, token) => api.SetSubscriptionAsync(summary.Community.Id, follow, token));

        // The post's own box is always there rather than opening on demand: it is the primary thing
        // to do on this page, and a button that reveals a box is one tap more for every comment.
        Composer = new CommentComposerViewModel(ComposerPurpose.Comment, PostCommentAsync);

        // Built once and handed down the whole thread: every comment renders pictures the same way.
        Media = new MarkdownMedia(services.ImageLoader, OpenPictureCommand);

        Votes = new VoteBarViewModel(
            new VoteOutcome(summary.MyVote, summary.Tally.Score, summary.Tally.Upvotes, summary.Tally.Downvotes),
            api.IsAuthenticated,
            (vote, token) => api.VoteOnPostAsync(summary.Post.Id, vote, token));
    }

    /// <summary>The post and everything joined onto it.</summary>
    public PostSummary Summary { get; }

    /// <summary>The arrows and the running score for the post itself.</summary>
    public VoteBarViewModel Votes { get; }

    /// <summary>The subscribe control for the community the post is in.</summary>
    public SubscribeButtonViewModel Subscription { get; }

    /// <summary>The box for adding a comment to the post.</summary>
    public CommentComposerViewModel Composer { get; }

    /// <summary>How pictures written into the post or its comments are fetched and opened.</summary>
    public MarkdownMedia Media { get; }

    /// <summary>Whether anybody is signed in to comment at all.</summary>
    public bool CanComment => api.IsAuthenticated && !Summary.Post.IsLocked;

    /// <inheritdoc />
    public override string Title => Summary.Post.Title.Value;

    /// <summary>The community, in <c>!name@instance</c> form.</summary>
    public string CommunityLabel => Summary.Community.QualifiedName;

    /// <summary>The author's display name.</summary>
    public string AuthorLabel => Summary.Creator.PreferredName;

    /// <summary>How long ago the post was made.</summary>
    public string AgeLabel { get; }

    /// <summary>The score, shortened for a badge.</summary>
    public string ScoreLabel => Summary.Tally.Score.ToCompactString();

    /// <summary>The comment count, shortened for a badge.</summary>
    public string CommentsLabel => Summary.Tally.Comments.ToCompactString();

    /// <summary>The post body, parsed into blocks for the renderer.</summary>
    public ImmutableArray<MarkdownBlock> Body { get; }

    /// <summary>Whether there is a body to render.</summary>
    public bool HasBody => !Summary.Post.Body.IsEmpty;

    /// <summary>The link the post points at, or empty for a self post.</summary>
    public string LinkLabel => Summary.Post.Url is { } url ? url.Value : string.Empty;

    /// <summary>Whether the post links somewhere.</summary>
    public bool HasLink => Summary.Post.Url.HasValue;

    /// <summary>Whether the post can be opened on the web; every federated post has an address.</summary>
    public bool HasWebAddress => Summary.Post.ActorId.IsValid;

    /// <summary>Whether the post carries an image.</summary>
    public bool HasImage => Summary.Post.PreviewImage.HasValue;

    /// <summary>Whether comments are closed.</summary>
    public bool IsLocked => Summary.Post.IsLocked;

    /// <summary>The comment trees, in server order.</summary>
    public ObservableCollection<CommentViewModel> Comments { get; } = [];

    /// <summary>The comment sorts offered in the toolbar.</summary>
    public static ReadOnlyCollection<CommentSortOption> SortOptions => DisplayOptions.CommentSorts;

    /// <summary>The comment sort currently applied.</summary>
    [ObservableProperty]
    private CommentSortType selectedSort;

    /// <summary>The post's image, once decoded.</summary>
    [ObservableProperty]
    private Bitmap? image;

    /// <summary>Whether the image is covered because the post is flagged not safe for work.</summary>
    [ObservableProperty]
    private bool isImageHidden;

    /// <summary>Whether the thread came back with nothing in it.</summary>
    [ObservableProperty]
    private bool hasNoComments;

    /// <inheritdoc />
    public override async Task LoadAsync()
    {
        // The image and the comments are independent; neither should wait on the other.
        Task imageTask = LoadImageAsync();
        await ReloadCommentsAsync().ConfigureAwait(true);
        await imageTask.ConfigureAwait(true);
    }

    /// <summary>Fetches the comment thread with the current sort.</summary>
    [RelayCommand]
    public Task ReloadCommentsAsync() => RunAsync(async cancellationToken =>
    {
        Comments.Clear();
        HasNoComments = false;

        var query = new CommentQuery(Summary.Post.Id, SelectedSort, ThreadDepth, PageSize.Clamp(PageSize.Maximum));
        CommentThread thread = await api.GetCommentsAsync(query, cancellationToken).ConfigureAwait(true);

        DateTimeOffset now = Services.Now;
        foreach (CommentNode root in thread.Roots)
        {
            Comments.Add(new CommentViewModel(root, now, api, Services.Account, Media, Services.Copier, Navigator, OpenMarkdownLinkCommand));
        }

        HasNoComments = Comments.Count == 0;
    });

    /// <summary>Opens a link the reader pressed inside the body or a comment.</summary>
    [RelayCommand]
    private async Task OpenMarkdownLinkAsync(WebLink link) =>
        await Services.LinkOpener.OpenAsync(link).ConfigureAwait(true);

    /// <summary>Opens the post's link in the browser.</summary>
    [RelayCommand(CanExecute = nameof(HasLink))]
    private async Task OpenLinkAsync()
    {
        if (Summary.Post.Url is { } url)
        {
            await Services.LinkOpener.OpenAsync(url).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Opens the post itself on its home instance. Worth having in a client that only reads: voting,
    /// commenting and subscribing all live on the other side of this link.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasWebAddress))]
    private async Task OpenOnTheWebAsync() =>
        await Services.LinkOpener.OpenAsync(Summary.Post.ActorId.Link).ConfigureAwait(true);

    /// <summary>Uncovers an image hidden for being flagged not safe for work.</summary>
    [RelayCommand]
    private void RevealImage() => IsImageHidden = false;

    /// <summary>Opens the community this post was made in.</summary>
    [RelayCommand]
    private void OpenCommunity()
    {
        var summary = new CommunitySummary(Summary.Community, CommunityTally.Empty);
        Navigator.Push(new FeedViewModel(Services, Navigator, api, settings, summary));
    }

    partial void OnSelectedSortChanged(CommentSortType value)
    {
        _ = value;
        _ = ReloadCommentsAsync();
    }

    private async Task LoadImageAsync()
    {
        if (Summary.Post.PreviewImage is not { } link)
        {
            return;
        }

        Image = await Services.ImageLoader.LoadAsync(link, ImageDecodeWidth, Lifetime).ConfigureAwait(true);
    }

    /// <summary>Opens the author's page.</summary>
    [RelayCommand]
    private void OpenAuthor() => Navigator.ShowProfile(Summary.Creator.Id);

    /// <summary>Copies the post's body as the Markdown it was written in.</summary>
    [RelayCommand]
    private async Task CopyBodyAsync() =>
        WasCopied = await Services.Copier.CopyAsync(Summary.Post.Body.Value).ConfigureAwait(true);

    /// <summary>Set once a copy succeeds.</summary>
    [ObservableProperty]
    private bool wasCopied;

    /// <summary>Opens a picture from a body full screen, where it can be zoomed.</summary>
    [RelayCommand]
    private void OpenPicture(MarkdownImage image) => Navigator.ShowPicture(image.Source, image.AltText);

    private async Task PostCommentAsync(CommentDraft draft, CancellationToken cancellationToken)
    {
        CommentNode posted = await api
            .CreateCommentAsync(Summary.Post.Id, null, draft, cancellationToken)
            .ConfigureAwait(true);

        // At the top, whatever the thread is sorted by: the sort is the server's answer to a
        // question asked before this comment existed.
        Comments.Insert(0, new CommentViewModel(posted, Services.Now, api, Services.Account, Media, Services.Copier, Navigator, OpenMarkdownLinkCommand));
        HasNoComments = false;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Subscription.Dispose();
        }

        base.Dispose(disposing);
    }
}
