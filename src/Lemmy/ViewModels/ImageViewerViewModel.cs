using System.Collections.Immutable;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>
/// The pictures on a page, full screen, one at a time. Opened straight from a feed row and moved
/// through without going back to the feed, because browsing an art or comic community is a sequence
/// of pictures rather than a sequence of posts.
/// </summary>
public sealed partial class ImageViewerViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// Decoded well above screen width so that zooming into a comic panel still has pixels to show.
    /// The bitmap is not cached and is disposed on the way to the next picture, so the cost is one
    /// image at a time rather than the whole gallery.
    /// </summary>
    private const int DecodeWidth = 2560;

    /// <summary>
    /// How much decoded picture to hold beyond the one on screen. One neighbour is enough to make a
    /// flick feel instant; more than that is memory spent on pictures the reader may never reach,
    /// and on a phone that is memory the system will take back at the worst moment.
    /// </summary>
    private const long PrefetchBudgetInBytes = 64L * 1024 * 1024;

    /// <summary>
    /// How many further batches to ask for while looking for one more picture. A page can be all
    /// articles, so one batch is not always enough; without a limit a picture-free feed would be
    /// paged to its end in one go.
    /// </summary>
    private const int MaxExtendAttempts = 3;

    private readonly IImageGallery gallery;
    private readonly IImageLoader imageLoader;
    private readonly Action close;
    private readonly CancellationTokenSource lifetime = new();

    /// <summary>Identifies the picture being shown, so its place is found afresh in a growing list.</summary>
    private PostId currentId;

    /// <summary>Distinguishes the load that should win when the reader flicks faster than the network.</summary>
    private int loadGeneration;

    private AnimatedImage? picture;

    /// <summary>The neighbour fetched ahead of time, and which post it belongs to.</summary>
    private AnimatedImage? prefetched;
    private PostId prefetchedId;
    private CancellationTokenSource? prefetchLifetime;

    /// <summary>Which way the reader is going, so the fetch goes the same way. Forward to begin with.</summary>
    private int direction = 1;

    private bool isDisposed;

    /// <summary>Opens the viewer on one picture from a page of them.</summary>
    /// <param name="gallery">The page the picture came from.</param>
    /// <param name="summary">The picture to show first.</param>
    /// <param name="imageLoader">Fetches and decodes at full resolution.</param>
    /// <param name="close">Called when the reader dismisses the viewer.</param>
    public ImageViewerViewModel(IImageGallery gallery, PostSummary summary, IImageLoader imageLoader, Action close)
    {
        ArgumentNullException.ThrowIfNull(gallery);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(imageLoader);
        ArgumentNullException.ThrowIfNull(close);

        this.gallery = gallery;
        this.imageLoader = imageLoader;
        this.close = close;

        Summary = summary;
        currentId = summary.Id;
    }

    /// <summary>The post whose picture is on screen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(CommunityLabel))]
    private PostSummary summary;

    /// <summary>The picture being shown, at its original resolution.</summary>
    public WebLink? Link => Summary.Post.FullImage;

    /// <summary>The post's headline, shown over the picture.</summary>
    public string Title => Summary.Post.Title.Value;

    /// <summary>Where it was posted, so the reader knows what they are looking at.</summary>
    public string CommunityLabel => Summary.Community.QualifiedName;

    /// <summary>The frame currently on screen — the whole picture when it does not animate.</summary>
    [ObservableProperty]
    private Bitmap? image;

    /// <summary>Whether what is on screen has more than one frame.</summary>
    [ObservableProperty]
    private bool isAnimated;

    /// <summary>Whether the picture is still being fetched.</summary>
    [ObservableProperty]
    private bool isLoading = true;

    /// <summary>Whether the picture could not be loaded at all.</summary>
    [ObservableProperty]
    private bool hasFailed;

    /// <summary>
    /// Whether what is on screen is the server's preview rather than the original, because the
    /// original is in a format this build cannot decode.
    /// </summary>
    [ObservableProperty]
    private bool isShowingReducedQuality;

    /// <summary>How far the reader has zoomed and panned.</summary>
    [ObservableProperty]
    private ZoomState zoom = ZoomState.Fitted;

    /// <summary>Where this picture sits in the page, e.g. <c>3 / 17</c>; empty when it is the only one.</summary>
    [ObservableProperty]
    private string positionLabel = string.Empty;

    /// <summary>Whether there is a picture before this one.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShowPreviousCommand))]
    private bool canShowPrevious;

    /// <summary>Whether there is a picture after this one.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShowNextCommand))]
    private bool canShowNext;

    /// <summary>Whether the page is fetching more posts to carry on into.</summary>
    [ObservableProperty]
    private bool isLoadingMore;

    /// <summary>Fetches the current picture at full resolution.</summary>
    public async Task LoadAsync()
    {
        UpdatePosition();

        if (Link is not { } link)
        {
            IsLoading = false;
            HasFailed = true;
            return;
        }

        int generation = ++loadGeneration;

        // Already fetched while the reader was looking at the last one: show it and skip the wait.
        if (TakePrefetched() is { } ready)
        {
            picture?.Dispose();
            picture = ready;

            Image = ready.FirstFrame;
            IsAnimated = ready.IsAnimated;
            IsShowingReducedQuality = false;
            HasFailed = false;
            IsLoading = false;

            StartPrefetch();
            _ = ExtendGalleryAsync();
            return;
        }

        IsLoading = true;
        HasFailed = false;

        AnimatedImage? loaded = await imageLoader.LoadPictureAsync(link, DecodeWidth, lifetime.Token).ConfigureAwait(true);
        bool isReduced = false;

        // Some originals are in formats the platform cannot decode — AVIF is the one that turns up
        // in practice. The server's own thumbnail is always in a format it can, so a softer picture
        // beats no picture; the caption says so rather than pretending it is the real thing.
        if (loaded is null && Summary.Post.Thumbnail is { } thumbnail && thumbnail != link)
        {
            loaded = await imageLoader.LoadPictureAsync(thumbnail, DecodeWidth, lifetime.Token).ConfigureAwait(true);
            isReduced = loaded is not null;
        }

        // A faster flick than the network: this picture is no longer the one on screen.
        if (isDisposed || generation != loadGeneration)
        {
            loaded?.Dispose();
            return;
        }

        picture?.Dispose();
        picture = loaded;

        Image = loaded?.FirstFrame;
        IsAnimated = loaded?.IsAnimated ?? false;
        IsShowingReducedQuality = isReduced;
        HasFailed = loaded is null;
        IsLoading = false;

        StartPrefetch();
        _ = ExtendGalleryAsync();
    }

    /// <summary>
    /// Asks the page for more posts once the reader reaches its last picture, so the carousel keeps
    /// going rather than stopping at whatever the feed happened to have loaded. Fire-and-forget: it
    /// runs behind the picture already on screen.
    /// </summary>
    private async Task ExtendGalleryAsync()
    {
        if (isDisposed || IsLoadingMore || CanShowNext || !gallery.CanLoadMore)
        {
            return;
        }

        IsLoadingMore = true;
        try
        {
            for (int attempt = 0; attempt < MaxExtendAttempts; attempt++)
            {
                await gallery.LoadMoreAsync().ConfigureAwait(true);

                if (isDisposed)
                {
                    return;
                }

                UpdatePosition();

                // A batch of nothing but articles adds no pictures, so try again — up to a point.
                if (CanShowNext || !gallery.CanLoadMore)
                {
                    break;
                }
            }
        }
        finally
        {
            IsLoadingMore = false;
        }

        if (CanShowNext)
        {
            StartPrefetch();
        }
    }

    /// <summary>
    /// Fetches the neighbour the reader is heading towards, so the next flick shows immediately.
    /// Deliberately fire-and-forget: nothing waits on it, and if the reader moves first the result
    /// is either adopted or thrown away.
    /// </summary>
    private void StartPrefetch()
    {
        CancelPrefetch();

        if (isDisposed || picture is null)
        {
            return;
        }

        ImmutableArray<PostSummary> images = gallery.Images;
        int index = IndexOf(images, currentId);
        int target = index + direction;

        if (index < 0 || target < 0 || target >= images.Length)
        {
            return;
        }

        PostSummary neighbour = images[target];
        if (neighbour.Post.FullImage is not { } link)
        {
            return;
        }

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        prefetchLifetime = cancellation;
        prefetchedId = neighbour.Id;

        _ = PrefetchAsync(link, neighbour.Id, cancellation.Token);
    }

    private async Task PrefetchAsync(WebLink link, PostId id, CancellationToken cancellationToken)
    {
        AnimatedImage? loaded = await imageLoader.LoadPictureAsync(link, DecodeWidth, cancellationToken).ConfigureAwait(true);

        if (loaded is null)
        {
            return;
        }

        // Keep it only if it is still the neighbour we want and the pair fits the budget. A long
        // animation or a huge scan can exceed that on its own, and holding two of those is how a
        // phone kills the app mid-scroll.
        bool isWanted = !isDisposed && prefetchedId == id && cancellationToken == prefetchLifetime?.Token;
        bool fits = (picture?.EstimatedBytes ?? 0) + loaded.EstimatedBytes <= PrefetchBudgetInBytes;

        if (isWanted && fits)
        {
            prefetched?.Dispose();
            prefetched = loaded;
            return;
        }

        loaded.Dispose();
    }

    /// <summary>Hands over the prefetched picture when it is the one now wanted.</summary>
    private AnimatedImage? TakePrefetched()
    {
        if (prefetched is null || prefetchedId != currentId)
        {
            return null;
        }

        AnimatedImage ready = prefetched;
        prefetched = null;
        prefetchedId = default;

        return ready;
    }

    private void CancelPrefetch()
    {
        prefetchLifetime?.Cancel();
        prefetchLifetime?.Dispose();
        prefetchLifetime = null;
    }

    /// <summary>Drops a prefetch that is no longer next to where the reader is.</summary>
    private void DiscardPrefetch()
    {
        CancelPrefetch();
        prefetched?.Dispose();
        prefetched = null;
        prefetchedId = default;
    }

    /// <summary>Moves to the previous picture on the page.</summary>
    [RelayCommand(CanExecute = nameof(CanShowPrevious))]
    public Task ShowPreviousAsync() => MoveAsync(-1);

    /// <summary>Moves to the next picture on the page.</summary>
    [RelayCommand(CanExecute = nameof(CanShowNext))]
    public Task ShowNextAsync() => MoveAsync(1);

    /// <summary>Dismisses the viewer.</summary>
    [RelayCommand]
    public void Close() => close();

    /// <summary>Returns to showing the whole picture.</summary>
    [RelayCommand]
    private void ResetZoom() => Zoom = ZoomState.Fitted;

    /// <summary>Applies a gesture result, keeping the view and the view model in step.</summary>
    public void Apply(ZoomState state) => Zoom = state;

    /// <summary>
    /// Shows whichever frame belongs at <paramref name="elapsed"/> into the animation. The clock
    /// belongs to the view; mapping time to a frame belongs here, where it can be tested.
    /// </summary>
    public void ShowFrameAt(TimeSpan elapsed)
    {
        if (picture is not { IsAnimated: true } animation)
        {
            return;
        }

        Image = animation.Frames[animation.FrameIndexAt(elapsed)].Image;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        if (!lifetime.IsCancellationRequested)
        {
            lifetime.Cancel();
        }

        DiscardPrefetch();
        lifetime.Dispose();

        // Nothing else shares these frames: the viewer loads outside the cache precisely so that a
        // full-resolution picture is released the moment it stops being looked at.
        Image = null;
        picture?.Dispose();
        picture = null;
    }

    private async Task MoveAsync(int step)
    {
        ImmutableArray<PostSummary> images = gallery.Images;
        int index = IndexOf(images, currentId);

        if (index < 0)
        {
            return;
        }

        int target = index + step;
        if (target < 0 || target >= images.Length)
        {
            return;
        }

        // Going the other way makes the picture just left the one worth having ready.
        if (Math.Sign(step) != direction)
        {
            direction = Math.Sign(step);
            DiscardPrefetch();
        }

        Summary = images[target];
        currentId = Summary.Id;

        // A new picture starts fitted to the screen; carrying the previous zoom over would drop the
        // reader into a random corner of it.
        Zoom = ZoomState.Fitted;
        IsShowingReducedQuality = false;
        IsAnimated = false;

        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Re-reads the page's pictures and works out where this one sits. Done on every move rather
    /// than once, so pictures loaded since the viewer opened are included.
    /// </summary>
    private void UpdatePosition()
    {
        ImmutableArray<PostSummary> images = gallery.Images;
        int index = IndexOf(images, currentId);

        CanShowPrevious = index > 0;
        CanShowNext = index >= 0 && index < images.Length - 1;

        PositionLabel = index < 0 || images.Length <= 1
            ? string.Empty
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{index + 1} / {images.Length}");
    }

    private static int IndexOf(ImmutableArray<PostSummary> images, PostId id)
    {
        for (int index = 0; index < images.Length; index++)
        {
            if (images[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }
}
