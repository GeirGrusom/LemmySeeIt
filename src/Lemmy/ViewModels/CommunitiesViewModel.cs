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

        query = new CommunityQuery(ListingType.Local, PostSortType.TopMonth, 1, PageSize.Clamp(PageSize.Maximum), settings.ShowNsfw);
        selectedListing = ListingType.Local;
    }

    /// <inheritdoc />
    public override string Title => "Communities";

    /// <summary>The communities loaded so far.</summary>
    public ObservableCollection<CommunityRowViewModel> Communities { get; } = [];

    /// <summary>The listings offered in the toolbar.</summary>
    public static ReadOnlyCollection<ListingOption> ListingOptions => DisplayOptions.Listings;

    /// <summary>Whether the directory is showing local or all known communities.</summary>
    [ObservableProperty]
    private ListingType selectedListing;

    /// <summary>Whether another page is being fetched.</summary>
    [ObservableProperty]
    private bool isLoadingMore;

    /// <inheritdoc />
    public override Task LoadAsync() => ReloadAsync();

    /// <summary>Discards what is loaded and fetches the first page again.</summary>
    [RelayCommand]
    public Task ReloadAsync() => RunAsync(async cancellationToken =>
    {
        // Fetch before discarding, so a pull-to-refresh keeps the list on screen throughout.
        query = query with { Page = 1 };
        ImmutableArray<CommunitySummary> page = await api.GetCommunitiesAsync(query, cancellationToken).ConfigureAwait(true);

        Communities.Clear();
        reachedEnd = false;
        Append(page);
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
        _ = ReloadAsync();
    }

    private void Append(ImmutableArray<CommunitySummary> page)
    {
        foreach (CommunitySummary summary in page)
        {
            Communities.Add(new CommunityRowViewModel(summary, Open));
        }

        // A short page is the only end-of-list signal a numbered pager gives us.
        reachedEnd = page.Length < query.PageSize.Value;
    }

    private void Open(CommunityRowViewModel row) =>
        Navigator.Push(new FeedViewModel(Services, Navigator, api, settings, row.Summary));
}
