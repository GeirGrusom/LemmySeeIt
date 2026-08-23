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
/// Searching one instance. Runs only when asked: Lemmy's search is expensive enough that firing it
/// on every keystroke would be rude to the server and slow for the reader.
/// </summary>
public sealed partial class SearchViewModel : PageViewModel, IImageGallery
{
    private readonly ILemmyApi api;
    private readonly AppSettings settings;

    /// <summary>Creates the search page for an instance.</summary>
    public SearchViewModel(AppServices services, INavigator navigator, ILemmyApi api, AppSettings settings)
        : base(services, navigator)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(settings);

        this.api = api;
        this.settings = settings;
    }

    /// <inheritdoc />
    public override string Title => "Search";

    /// <summary>Matching posts.</summary>
    public ObservableCollection<PostCardViewModel> Posts { get; } = [];

    /// <inheritdoc />
    public ImmutableArray<PostSummary> Images =>
        [.. Posts.Where(card => card.CanViewImage).Select(card => card.Summary)];

    /// <summary>Search returns one page and no more; there is nothing further to reach.</summary>
    public bool CanLoadMore => false;

    /// <inheritdoc />
    public Task LoadMoreAsync() => Task.CompletedTask;

    /// <summary>Matching communities.</summary>
    public ObservableCollection<CommunityRowViewModel> Communities { get; } = [];

    /// <summary>What the reader typed.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private string queryText = string.Empty;

    /// <summary>Whether a search has run and found nothing.</summary>
    [ObservableProperty]
    private bool foundNothing;

    /// <summary>Whether any search has run yet.</summary>
    [ObservableProperty]
    private bool hasSearched;

    /// <summary>Whether the last search matched any posts.</summary>
    [ObservableProperty]
    private bool hasPostResults;

    /// <summary>Whether the last search matched any communities.</summary>
    [ObservableProperty]
    private bool hasCommunityResults;

    /// <inheritdoc />
    public override Task LoadAsync() => Task.CompletedTask;

    /// <summary>Whether the typed text is long enough to search for.</summary>
    public bool CanSearch => SearchTerm.TryCreate(QueryText.AsSpan(), out _);

    /// <summary>Runs the search.</summary>
    [RelayCommand(CanExecute = nameof(CanSearch))]
    public Task SearchAsync() => RunAsync(async cancellationToken =>
    {
        if (!SearchTerm.TryCreate(QueryText.AsSpan(), out SearchTerm term))
        {
            return;
        }

        ClearResults();
        HasSearched = true;

        var query = new SearchQuery(term, SearchKind.All, ListingType.All, PostSortType.TopAll, null, 1, PageSize.Clamp(25));
        SearchResults results = await api.SearchAsync(query, cancellationToken).ConfigureAwait(true);

        DateTimeOffset now = Services.Now;
        foreach (PostSummary summary in results.Posts)
        {
            var card = new PostCardViewModel(summary, Services.ImageLoader, now, settings.BlurNsfwImages, api, Services.Subscriptions, Navigator, OpenPost, ViewImage);
            Posts.Add(card);
            _ = card.LoadThumbnailAsync();
        }

        foreach (CommunitySummary summary in results.Communities)
        {
            Communities.Add(new CommunityRowViewModel(summary, api, Services.Subscriptions, OpenCommunity));
        }

        HasPostResults = Posts.Count > 0;
        HasCommunityResults = Communities.Count > 0;
        FoundNothing = results.IsEmpty;
    });

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearResults();
        }

        base.Dispose(disposing);
    }

    private void ClearResults()
    {
        foreach (PostCardViewModel card in Posts)
        {
            card.Dispose();
        }

        Posts.Clear();

        foreach (CommunityRowViewModel row in Communities)
        {
            row.Dispose();
        }

        Communities.Clear();
        HasPostResults = false;
        HasCommunityResults = false;
        FoundNothing = false;
    }

    private void ViewImage(PostCardViewModel card) => Navigator.ShowImage(card.Summary, this);

    private void OpenPost(PostCardViewModel card) =>
        Navigator.Push(new PostDetailViewModel(Services, Navigator, api, card.Summary, settings));

    private void OpenCommunity(CommunityRowViewModel row) =>
        Navigator.Push(new FeedViewModel(Services, Navigator, api, settings, row.Summary));
}
