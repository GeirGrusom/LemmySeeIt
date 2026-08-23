using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>
/// A scrolling list of posts: the instance front page, or a single community's. Paging is by cursor
/// and strictly sequential — a second request is refused while one is in flight, because two
/// overlapping pages would interleave posts in the list.
/// </summary>
public sealed partial class FeedViewModel : PageViewModel, IImageGallery
{
    private readonly ILemmyApi api;
    private readonly AppSettings settings;
    private readonly CommunitySummary? community;

    private FeedQuery query;
    private PageCursor? nextCursor;
    private bool reachedEnd;

    /// <summary>Creates a feed over a whole instance, or over one community.</summary>
    /// <param name="services">The app's services.</param>
    /// <param name="navigator">Where opening a post goes.</param>
    /// <param name="api">The instance to read.</param>
    /// <param name="settings">The reader's saved preferences.</param>
    /// <param name="community">A community to restrict the feed to, or <see langword="null"/> for the front page.</param>
    public FeedViewModel(
        AppServices services,
        INavigator navigator,
        ILemmyApi api,
        AppSettings settings,
        CommunitySummary? community = null)
        : base(services, navigator)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(settings);

        this.api = api;
        this.settings = settings;
        this.community = community;

        ListingType listing = community is not null
            ? ListingType.All
            : Available(settings.Listing, api.IsAuthenticated);

        query = new FeedQuery(listing, settings.Sort, community?.Id, null, PageSize.Default, settings.ShowNsfw);

        // Only a community's own feed has a community to subscribe to; the front page does not.
        Subscription = community is null
            ? null
            : new SubscribeButtonViewModel(
                community.Community.Id,
                community.Subscription,
                api.IsAuthenticated,
                services.Subscriptions,
                (follow, token) => api.SetSubscriptionAsync(community.Community.Id, follow, token));

        selectedSort = settings.Sort;
        selectedListing = listing;
    }

    /// <summary>
    /// Keeps a saved listing only if it still makes sense. A reader who chose Subscribed and then
    /// signed out would otherwise be shown an empty feed with no explanation.
    /// </summary>
    private static ListingType Available(ListingType saved, bool isAuthenticated) =>
        saved is ListingType.Subscribed or ListingType.ModeratorView && !isAuthenticated
            ? ListingType.All
            : saved;

    /// <inheritdoc />
    public override string Title => community?.Community.PreferredTitle ?? "Front page";

    /// <summary>Whether this feed is one community rather than the whole instance.</summary>
    public bool IsCommunityFeed => community is not null;

    /// <summary>The subscribe control, or <see langword="null"/> on the front page.</summary>
    public SubscribeButtonViewModel? Subscription { get; }

    /// <summary>The community's subtitle line; empty on the front page, where the header says it already.</summary>
    public string SubtitleLabel =>
        community is null
            ? string.Empty
            : $"{community.Community.QualifiedName} · {community.Tally.Subscribers.ToCompactString()} subscribers";

    /// <summary>The posts loaded so far, oldest page first.</summary>
    public ObservableCollection<PostCardViewModel> Posts { get; } = [];

    /// <inheritdoc />
    public ImmutableArray<PostSummary> Images =>
        [.. Posts.Where(card => card.CanViewImage).Select(card => card.Summary)];

    /// <inheritdoc />
    public bool CanLoadMore => !IsDisposed && !reachedEnd;

    /// <summary>The sorts offered in the toolbar.</summary>
    public static ReadOnlyCollection<SortOption> SortOptions => DisplayOptions.PostSorts;

    /// <summary>The listings offered in the toolbar, which depends on whether anyone is signed in.</summary>
    public ReadOnlyCollection<ListingOption> ListingOptions =>
        api.IsAuthenticated ? DisplayOptions.SignedInListings : DisplayOptions.Listings;

    /// <summary>The sort currently applied.</summary>
    [ObservableProperty]
    private PostSortType selectedSort;

    /// <summary>The listing currently applied; ignored on a community feed.</summary>
    [ObservableProperty]
    private ListingType selectedListing;

    /// <summary>Whether another page is being fetched below the current one.</summary>
    [ObservableProperty]
    private bool isLoadingMore;

    /// <summary>Whether the feed has run out of posts.</summary>
    public bool HasReachedEnd => reachedEnd;

    /// <summary>
    /// Whether to show the big centred spinner. Only for a genuinely empty feed: during a refresh
    /// the rows are still on screen and the pull gesture has a spinner of its own, so a second one
    /// over the top of the content would be noise.
    /// </summary>
    public bool IsLoadingFirstPage => IsBusy && Posts.Count == 0;

    /// <inheritdoc />
    public override Task LoadAsync() => ReloadAsync();

    /// <summary>Discards what is loaded and fetches the first page again.</summary>
    [RelayCommand]
    public Task ReloadAsync() => RunAsync(async cancellationToken =>
    {
        // Fetch before discarding. Pull-to-refresh would otherwise blank the feed for the length of
        // a round trip, and a refresh that fails would leave the reader with nothing at all.
        PostPage page = await api.GetFeedAsync(query.Rewound(), cancellationToken).ConfigureAwait(true);

        ClearPosts();
        reachedEnd = false;
        nextCursor = null;
        Append(page);
    });

    /// <summary>
    /// Fetches the next page. Safe to call from a scroll handler: it returns immediately when a
    /// request is already running, when the feed has ended, or when the first page has not landed.
    /// </summary>
    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (IsDisposed || IsBusy || IsLoadingMore || reachedEnd || nextCursor is not { } cursor)
        {
            return;
        }

        IsLoadingMore = true;
        try
        {
            PostPage page = await api.GetFeedAsync(query.Next(cursor), Lifetime).ConfigureAwait(true);
            Append(page);
        }
        catch (OperationCanceledException)
        {
            // The page went away.
        }
        catch (LemmyApiException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    /// <inheritdoc />
    protected override void OnBusyChanged(bool isBusy) => OnPropertyChanged(nameof(IsLoadingFirstPage));

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearPosts();
            Subscription?.Dispose();
        }

        base.Dispose(disposing);
    }

    partial void OnSelectedSortChanged(PostSortType value)
    {
        if (query.Sort == value)
        {
            return;
        }

        query = query with { Sort = value };
        _ = ReloadAsync();
    }

    partial void OnSelectedListingChanged(ListingType value)
    {
        if (IsCommunityFeed || query.Listing == value)
        {
            return;
        }

        query = query with { Listing = value };
        _ = ReloadAsync();
    }

    private void Append(PostPage page)
    {
        foreach (PostSummary summary in page.Posts)
        {
            var card = new PostCardViewModel(summary, Services.ImageLoader, Services.Now, settings.BlurNsfwImages, api, Services.Subscriptions, Navigator, OpenPost, ViewImage);
            Posts.Add(card);

            // Deliberately not awaited: the row is already on screen, and the image can catch up.
            _ = card.LoadThumbnailAsync();
        }

        nextCursor = page.NextCursor;

        // An empty page with no cursor is how Lemmy says "that's all there is".
        reachedEnd = !page.HasMore;
        OnPropertyChanged(nameof(HasReachedEnd));
        OnPropertyChanged(nameof(IsLoadingFirstPage));
    }

    private void ClearPosts()
    {
        foreach (PostCardViewModel card in Posts)
        {
            card.Dispose();
        }

        Posts.Clear();
    }

    private void ViewImage(PostCardViewModel card) => Navigator.ShowImage(card.Summary, this);

    private void OpenPost(PostCardViewModel card) =>
        Navigator.Push(new PostDetailViewModel(Services, Navigator, api, card.Summary, settings));
}
