using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>
/// One row in a feed. Everything the row displays is computed once here rather than in the view, so
/// a compiled binding is a plain property read and scrolling does no formatting work.
/// </summary>
public sealed partial class PostCardViewModel : ViewModelBase, IDisposable
{
    /// <summary>Thumbnails are drawn small; decoding them any larger just burns memory.</summary>
    private const int ThumbnailDecodeWidth = 320;

    private readonly IImageLoader imageLoader;
    private readonly Action<PostCardViewModel> openRequested;
    private readonly Action<PostCardViewModel> viewImageRequested;
    private readonly CancellationTokenSource lifetime = new();

    /// <summary>Wraps a post for display.</summary>
    /// <param name="summary">The post and its joined rows.</param>
    /// <param name="imageLoader">Fetches the thumbnail.</param>
    /// <param name="now">The current time, for the age label.</param>
    /// <param name="blurNsfw">Whether images on posts flagged not safe for work start hidden.</param>
    /// <param name="openRequested">Called when the reader opens the post.</param>
    /// <param name="viewImageRequested">Called when the reader opens the picture without the post.</param>
    public PostCardViewModel(
        PostSummary summary,
        IImageLoader imageLoader,
        DateTimeOffset now,
        bool blurNsfw,
        Action<PostCardViewModel> openRequested,
        Action<PostCardViewModel> viewImageRequested)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(imageLoader);
        ArgumentNullException.ThrowIfNull(openRequested);
        ArgumentNullException.ThrowIfNull(viewImageRequested);

        Summary = summary;
        this.imageLoader = imageLoader;
        this.openRequested = openRequested;
        this.viewImageRequested = viewImageRequested;

        isImageHidden = blurNsfw && summary.Post.IsNsfw;
        AgeLabel = RelativeTime.Format(summary.Post.Published, now);
    }

    /// <summary>The post and everything joined onto it.</summary>
    public PostSummary Summary { get; }

    /// <summary>The post's identifier.</summary>
    public PostId Id => Summary.Post.Id;

    /// <summary>The headline.</summary>
    public string Title => Summary.Post.Title.Value;

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

    /// <summary>The host the post links to, or empty for a self post.</summary>
    public string LinkHostLabel => Summary.Post.Url is { } url ? url.Host.ToString() : string.Empty;

    /// <summary>Whether the post links somewhere.</summary>
    public bool HasLink => Summary.Post.Url.HasValue;

    /// <summary>A one-line flattening of the body, or empty for a link post.</summary>
    public string BodyPreview => Summary.Post.Body.ToPreview(220);

    /// <summary>Whether there is body text worth previewing.</summary>
    public bool HasBodyPreview => !Summary.Post.Body.IsEmpty;

    /// <summary>Whether the post carries an image to show.</summary>
    public bool HasImage => Summary.Post.PreviewImage.HasValue;

    /// <summary>Whether the post is flagged not safe for work.</summary>
    public bool IsNsfw => Summary.Post.IsNsfw;

    /// <summary>Whether the post is pinned.</summary>
    public bool IsFeatured => Summary.Post.IsFeatured;

    /// <summary>Whether comments are closed.</summary>
    public bool IsLocked => Summary.Post.IsLocked;

    /// <summary>Whether the author moderates the community or administers the instance.</summary>
    public bool HasAuthorBadge => Summary.CreatorIsModerator || Summary.CreatorIsAdmin;

    /// <summary>What that badge says.</summary>
    public string AuthorBadgeLabel => Summary.CreatorIsAdmin ? "ADMIN" : "MOD";

    /// <summary>The decoded thumbnail, once it has arrived.</summary>
    [ObservableProperty]
    private Bitmap? thumbnail;

    /// <summary>Whether the image is covered because the post is flagged not safe for work.</summary>
    [ObservableProperty]
    private bool isImageHidden;

    /// <summary>
    /// Whether tapping the picture should open it rather than the post. False for a link post's
    /// scraped preview: that thumbnail is an illustration of an article, not the thing itself.
    /// </summary>
    public bool CanViewImage => Summary.Post.IsImage && Summary.Post.FullImage.HasValue;

    /// <summary>Opens the post.</summary>
    [RelayCommand]
    private void Open() => openRequested(this);

    /// <summary>Opens the picture full screen, without opening the post.</summary>
    [RelayCommand(CanExecute = nameof(CanViewImage))]
    private void ViewImage() => viewImageRequested(this);

    /// <summary>Uncovers an image hidden for being flagged not safe for work.</summary>
    [RelayCommand]
    private void RevealImage() => IsImageHidden = false;

    /// <summary>
    /// Fetches the thumbnail. Separate from the constructor so a feed can build a whole page of
    /// cards synchronously and then let the images arrive as they will.
    /// </summary>
    public async Task LoadThumbnailAsync()
    {
        if (Summary.Post.PreviewImage is not { } image)
        {
            return;
        }

        Thumbnail = await imageLoader.LoadAsync(image, ThumbnailDecodeWidth, lifetime.Token).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!lifetime.IsCancellationRequested)
        {
            lifetime.Cancel();
        }

        lifetime.Dispose();
    }
}
