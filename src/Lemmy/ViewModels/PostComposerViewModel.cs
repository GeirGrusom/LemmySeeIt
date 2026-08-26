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
/// The form for writing a post, used both for a new one and for rewriting one of the reader's own.
/// A whole page rather than an inline box like the comment composer: a post is four fields and a
/// community, which is more than fits above a feed on a phone, and it is worth being able to leave
/// by going back rather than by finding a cancel button.
/// </summary>
public sealed partial class PostComposerViewModel : PageViewModel
{
    /// <summary>How much of the limit has to be used before a counter is worth showing.</summary>
    private const int TitleWarningMargin = 40;

    private const int BodyWarningMargin = 500;

    private readonly ILemmyApi api;
    private readonly Action<PostSummary> completed;

    /// <summary>The post being rewritten, or <see langword="null"/> when this is a new one.</summary>
    private readonly PostId? editing;

    private PostComposerViewModel(
        AppServices services,
        INavigator navigator,
        ILemmyApi api,
        Action<PostSummary> completed,
        PostId? editing)
        : base(services, navigator)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(completed);

        this.api = api;
        this.completed = completed;
        this.editing = editing;
    }

    /// <summary>
    /// Opens an empty form.
    /// </summary>
    /// <param name="services">The app's services.</param>
    /// <param name="navigator">Where the finished post is opened.</param>
    /// <param name="api">The instance to post to.</param>
    /// <param name="community">Where it will go, when the reader came from a community; otherwise one is chosen here.</param>
    /// <param name="posted">Handed the new post once the server has taken it.</param>
    public static PostComposerViewModel ForNewPost(
        AppServices services,
        INavigator navigator,
        ILemmyApi api,
        CommunitySummary? community,
        Action<PostSummary> posted) =>
        new(services, navigator, api, posted, editing: null)
        {
            Community = community,

            // Nothing to write until there is somewhere to write it, so the picker opens first.
            IsChoosingCommunity = community is null,
        };

    /// <summary>
    /// Opens the form on an existing post, prefilled with what it says.
    /// </summary>
    /// <param name="services">The app's services.</param>
    /// <param name="navigator">Unused here, but every page has one.</param>
    /// <param name="api">The instance the post lives on.</param>
    /// <param name="post">The post to rewrite.</param>
    /// <param name="saved">Handed the rewritten post once the server has taken it.</param>
    public static PostComposerViewModel ForEdit(
        AppServices services,
        INavigator navigator,
        ILemmyApi api,
        PostSummary post,
        Action<PostSummary> saved)
    {
        ArgumentNullException.ThrowIfNull(post);

        return new PostComposerViewModel(services, navigator, api, saved, post.Post.Id)
        {
            // The tally is not shown here, and the community cannot be changed anyway.
            Community = new CommunitySummary(post.Community, CommunityTally.Empty),
            Headline = post.Post.Title.Value,
            Link = post.Post.Url?.Value ?? string.Empty,
            Body = post.Post.Body.Value,
            IsNsfw = post.Post.IsNsfw,
        };
    }

    /// <inheritdoc />
    public override string Title => editing is null ? "New post" : "Edit post";

    /// <summary>Whether this form is rewriting a post rather than making one.</summary>
    public bool IsEditing => editing is not null;

    /// <summary>Whether the community can still be chosen; Lemmy cannot move a post once it is made.</summary>
    public bool CanChooseCommunity => editing is null;

    /// <summary>The headline.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitleLengthLabel))]
    [NotifyPropertyChangedFor(nameof(IsTitleNearLimit))]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string headline = string.Empty;

    /// <summary>What the post links to, if anything.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LinkError))]
    [NotifyPropertyChangedFor(nameof(HasLinkError))]
    private string link = string.Empty;

    /// <summary>The Markdown body, which a link post may leave empty.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BodyLengthLabel))]
    [NotifyPropertyChangedFor(nameof(IsBodyNearLimit))]
    private string body = string.Empty;

    /// <summary>Whether to flag the post as not safe for work.</summary>
    [ObservableProperty]
    private bool isNsfw;

    /// <summary>Where the post will go.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommunityLabel))]
    [NotifyPropertyChangedFor(nameof(HasCommunity))]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private CommunitySummary? community;

    /// <summary>Whether the community list is open over the form.</summary>
    [ObservableProperty]
    private bool isChoosingCommunity;

    /// <summary>What to search the directory for; empty lists the reader's own communities.</summary>
    [ObservableProperty]
    private string communitySearch = string.Empty;

    /// <summary>Whether the community list came back with nothing in it.</summary>
    [ObservableProperty]
    private bool hasNoChoices;

    /// <summary>Set while the post is being sent.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private bool isSending;

    /// <summary>The communities offered in the picker.</summary>
    public ObservableCollection<CommunitySummary> Choices { get; } = [];

    /// <summary>Whether somewhere to post has been settled on.</summary>
    public bool HasCommunity => Community is not null;

    /// <summary>The community in <c>!name@instance</c> form, or the prompt to pick one.</summary>
    public string CommunityLabel => Community?.Community.QualifiedName ?? "Choose a community";

    /// <summary>What the send button says.</summary>
    public string SubmitLabel => editing is null ? "Post" : "Save";

    /// <summary>Why the link cannot be used, or <see langword="null"/>; a blank link is fine.</summary>
    public string? LinkError => PostDraft.ExplainLink(Link.AsSpan());

    /// <summary>Whether there is a link complaint to show.</summary>
    public bool HasLinkError => LinkError is not null;

    /// <summary>How much of the title's room is left.</summary>
    public string TitleLengthLabel => $"{Headline.Trim().Length} / {PostTitle.MaxLength}";

    /// <summary>Whether the title is close enough to the limit for the counter to be worth showing.</summary>
    public bool IsTitleNearLimit => Headline.Trim().Length > PostTitle.MaxLength - TitleWarningMargin;

    /// <summary>How much of the body's room is left.</summary>
    public string BodyLengthLabel => $"{Body.Trim().Length} / {PostDraft.MaxBodyLength}";

    /// <summary>Whether the body is close enough to the limit for the counter to be worth showing.</summary>
    public bool IsBodyNearLimit => Body.Trim().Length > PostDraft.MaxBodyLength - BodyWarningMargin;

    /// <summary>What to say when the picker has nothing to offer.</summary>
    public string EmptyChoicesLabel => CommunitySearch.Trim().Length == 0
        ? "You do not follow any communities yet. Search for one to post to."
        : "No communities matched that.";

    /// <inheritdoc />
    public override Task LoadAsync() =>
        IsChoosingCommunity ? FindCommunitiesAsync() : Task.CompletedTask;

    /// <summary>Opens the community list.</summary>
    [RelayCommand]
    private void ChooseCommunity()
    {
        IsChoosingCommunity = true;

        if (Choices.Count == 0)
        {
            _ = FindCommunitiesAsync();
        }
    }

    /// <summary>Closes the community list without changing anything.</summary>
    [RelayCommand]
    private void CancelChoosing() => IsChoosingCommunity = false;

    /// <summary>Settles on a community and returns to the form.</summary>
    [RelayCommand]
    private void PickCommunity(CommunitySummary? choice)
    {
        if (choice is null)
        {
            return;
        }

        Community = choice;
        IsChoosingCommunity = false;
    }

    /// <summary>
    /// Fills the picker. With nothing typed it lists what the reader already follows, which is where
    /// almost every post goes; typing searches the wider directory for somewhere new.
    /// </summary>
    [RelayCommand]
    private Task FindCommunitiesAsync() => RunAsync(async cancellationToken =>
    {
        ImmutableArray<CommunitySummary> found = SearchTerm.TryCreate(CommunitySearch.AsSpan(), out SearchTerm term)
            ? (await api.SearchAsync(
                new SearchQuery(term, SearchKind.Communities, ListingType.All, PostSortType.TopAll, null, 1, PageSize.Clamp(25)),
                cancellationToken).ConfigureAwait(true)).Communities
            : await api.GetCommunitiesAsync(
                // The reader's own subscriptions, including any flagged not safe for work: hiding
                // one they follow from the list of places they can post would just be baffling.
                new CommunityQuery(
                    api.IsAuthenticated ? ListingType.Subscribed : ListingType.Local,
                    PostSortType.TopMonth,
                    1,
                    PageSize.Clamp(PageSize.Maximum),
                    ShowNsfw: true),
                cancellationToken).ConfigureAwait(true);

        Choices.Clear();
        foreach (CommunitySummary choice in found)
        {
            Choices.Add(choice);
        }

        HasNoChoices = Choices.Count == 0;
        OnPropertyChanged(nameof(EmptyChoicesLabel));
    });

    /// <summary>Sends the post, and hands it on once the server has taken it.</summary>
    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        if (!PostDraft.TryCreate(Headline.AsSpan(), Link.AsSpan(), Body.AsSpan(), IsNsfw, out PostDraft draft))
        {
            ErrorMessage = PostDraft.Explain(Headline.AsSpan(), Link.AsSpan(), Body.AsSpan());
            return;
        }

        PostSummary? result = null;
        IsSending = true;
        ErrorMessage = null;

        try
        {
            result = await SendAsync(draft, Lifetime).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The page went away; what was typed goes with it.
        }
        catch (LemmyApiException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsSending = false;
        }

        // Outside the guard on purpose: handing the post on navigates away from this page, which
        // disposes it, so nothing may touch its state afterwards.
        if (result is not null)
        {
            completed(result);
        }
    }

    private Task<PostSummary> SendAsync(PostDraft draft, CancellationToken cancellationToken)
    {
        if (editing is { } postId)
        {
            return api.EditPostAsync(postId, draft, cancellationToken);
        }

        if (Community is not { } target)
        {
            throw new LemmyApiException("Choose a community to post to.");
        }

        return api.CreatePostAsync(target.Id, draft, cancellationToken);
    }

    private bool CanSubmit() =>
        !IsSending && Headline.Trim().Length > 0 && (IsEditing || HasCommunity);
}
