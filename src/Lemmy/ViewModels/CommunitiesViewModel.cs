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
/// The instance's community directory. Pages by number rather than cursor, because that is what
/// Lemmy's community endpoint offers.
/// </summary>
public sealed partial class CommunitiesViewModel : PageViewModel
{
    private readonly ILemmyApi api;
    private readonly AppSettings settings;

    private CommunityQuery query;
    private bool reachedEnd;

    /// <summary>Creates the directory for an instance.</summary>
    public CommunitiesViewModel(AppServices services, INavigator navigator, ILemmyApi api, AppSettings settings)
        : base(services, navigator)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(settings);

        this.api = api;
        this.settings = settings;

        // Signed in, the communities worth listing are the reader's own; finding new ones is what
        // Search is for. Signed out there are no subscriptions, so the instance's own list it is.
        ListingType opening = api.IsAuthenticated ? ListingType.Subscribed : ListingType.Local;

        query = new CommunityQuery(opening, PostSortType.TopMonth, 1, PageSize.Clamp(PageSize.Maximum), settings.ShowNsfw);
        selectedListing = opening;
    }

    /// <inheritdoc />
    public override string Title => "Communities";

    /// <summary>The communities loaded so far.</summary>
    public ObservableCollection<CommunityRowViewModel> Communities { get; } = [];

    /// <summary>
    /// The listings offered in the toolbar. Subscribed only exists once there is an account to have
    /// subscriptions, which is the same rule the feed uses.
    /// </summary>
    public ReadOnlyCollection<ListingOption> ListingOptions =>
        api.IsAuthenticated ? DisplayOptions.SignedInListings : DisplayOptions.Listings;

    /// <summary>Whether the directory is showing local or all known communities.</summary>
    [ObservableProperty]
    private ListingType selectedListing;

    /// <summary>Whether another page is being fetched.</summary>
    [ObservableProperty]
    private bool isLoadingMore;

    /// <summary>Whether the listing came back with nothing at all.</summary>
    [ObservableProperty]
    private bool hasNoCommunities;

    /// <summary>
    /// What to say when it did. An account that follows nothing opens on an empty Subscribed list,
    /// and a blank tab with no explanation reads as a broken one.
    /// </summary>
    public string EmptyLabel => SelectedListing == ListingType.Subscribed
        ? "You do not follow any communities yet. Search to find some."
        : "No communities to show here.";

    /// <inheritdoc />
    public override Task LoadAsync() => ReloadAsync();

    /// <summary>Discards what is loaded and fetches the first page again.</summary>
    [RelayCommand]
    public Task ReloadAsync() => RunAsync(async cancellationToken =>
    {
        // Fetch before discarding, so a pull-to-refresh keeps the list on screen throughout.
        query = query with { Page = 1 };
        ImmutableArray<CommunitySummary> page = await api.GetCommunitiesAsync(query, cancellationToken).ConfigureAwait(true);

        ClearCommunities();
        reachedEnd = false;
        Append(page);
        HasNoCommunities = Communities.Count == 0;
    });

    /// <summary>Fetches the next page of the directory.</summary>
    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (IsDisposed || IsBusy || IsLoadingMore || reachedEnd)
        {
            return;
        }

        IsLoadingMore = true;
        try
        {
            query = query with { Page = query.PageNumber + 1 };
            ImmutableArray<CommunitySummary> page = await api.GetCommunitiesAsync(query, Lifetime).ConfigureAwait(true);
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

    partial void OnSelectedListingChanged(ListingType value)
    {
        if (query.Listing == value)
        {
            return;
        }

        query = query with { Listing = value };
        OnPropertyChanged(nameof(EmptyLabel));
        _ = ReloadAsync();
    }

    private void Append(ImmutableArray<CommunitySummary> page)
    {
        foreach (CommunitySummary summary in page)
        {
            Communities.Add(new CommunityRowViewModel(summary, api, Services.Subscriptions, Open));
        }

        // A short page is the only end-of-list signal a numbered pager gives us.
        reachedEnd = page.Length < query.PageSize.Value;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearCommunities();
        }

        base.Dispose(disposing);
    }

    private void ClearCommunities()
    {
        foreach (CommunityRowViewModel row in Communities)
        {
            row.Dispose();
        }

        Communities.Clear();
    }

    private void Open(CommunityRowViewModel row) =>
        Navigator.Push(new FeedViewModel(Services, Navigator, api, settings, row.Summary));
}
