using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Markdown;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>
/// An account's page: who they are, what they wrote about themselves, and the most recent of what
/// they have written.
/// </summary>
/// <remarks>
/// Signing out lives here rather than behind the header control. It cannot be undone — the token is
/// invalidated on the server, so getting back in means typing a password — and a header whose
/// layout shifts as an account name replaces "Sign in" is no place for it. One stray tap once ended
/// a live session during testing, which is why it moved.
/// </remarks>
public sealed partial class ProfileViewModel : PageViewModel, IImageGallery
{
    private readonly ILemmyApi api;
    private readonly AppSettings settings;
    private readonly PersonId personId;
    private readonly Func<Task>? signOut;

    /// <summary>Creates the page for one account.</summary>
    /// <param name="services">The services every page needs.</param>
    /// <param name="navigator">Where this page sends the reader next.</param>
    /// <param name="api">The client this page reads through.</param>
    /// <param name="settings">What the reader chose, for image blurring.</param>
    /// <param name="personId">Whose page this is.</param>
    /// <param name="signOut">
    /// Ends the session, or <see langword="null"/> when this is not the reader's own page.
    /// </param>
    public ProfileViewModel(
        AppServices services,
        INavigator navigator,
        ILemmyApi api,
        AppSettings settings,
        PersonId personId,
        Func<Task>? signOut = null)
        : base(services, navigator)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(settings);

        this.api = api;
        this.settings = settings;
        this.personId = personId;
        this.signOut = signOut;
    }

    /// <inheritdoc />
    public override string Title => Profile?.Person.PreferredName ?? "Profile";

    /// <summary>The account, once it has arrived.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(QualifiedName))]
    [NotifyPropertyChangedFor(nameof(HasBio))]
    [NotifyPropertyChangedFor(nameof(IsAdmin))]
    [NotifyPropertyChangedFor(nameof(ActivityLabel))]
    [NotifyPropertyChangedFor(nameof(JoinedLabel))]
    [NotifyPropertyChangedFor(nameof(Avatar))]
    private PersonProfile? profile;

    /// <summary>The unambiguous <c>@name@instance</c> form.</summary>
    public string QualifiedName => Profile?.Person.QualifiedName ?? string.Empty;

    /// <summary>Their profile picture, when they have one.</summary>
    public WebLink? Avatar => Profile?.Person.Avatar;

    /// <summary>Whether they wrote anything about themselves.</summary>
    public bool HasBio => Profile?.HasBio ?? false;

    /// <summary>What they wrote about themselves, parsed.</summary>
    [ObservableProperty]
    private ImmutableArray<MarkdownBlock> bio = [];

    /// <summary>Whether they administer the instance.</summary>
    public bool IsAdmin => Profile?.IsAdmin ?? false;

    /// <summary>How much they have written, e.g. <c>12 posts · 340 comments</c>.</summary>
    public string ActivityLabel => Profile is not { } found
        ? string.Empty
        : $"{Count(found.Tally.Posts, "post")} · {Count(found.Tally.Comments, "comment")}";

    /// <summary>
    /// A count and its noun, kept in agreement. Exactly one is the only singular case: a shortened
    /// count like <c>1.2k</c> is plural however it reads.
    /// </summary>
    private static string Count(VoteCount value, string noun) =>
        value.Value == 1 ? $"1 {noun}" : $"{value.ToCompactString()} {noun}s";

    /// <summary>When the account was created.</summary>
    public string JoinedLabel => Profile is not { } found
        ? string.Empty
        : $"joined {RelativeTime.Format(found.Person.Published, Services.Now)}";

    /// <summary>Their recent posts.</summary>
    public ObservableCollection<PostCardViewModel> Posts { get; } = [];

    /// <summary>Their recent comments, each standing alone.</summary>
    public ObservableCollection<ProfileCommentViewModel> Comments { get; } = [];

    /// <summary>Whether they have posted anything the page could show.</summary>
    [ObservableProperty]
    private bool hasNothing;

    /// <summary>Whether this is the reader's own page, and so offers to sign out.</summary>
    public bool CanSignOut => signOut is not null;

    /// <summary>Set while the session is being ended.</summary>
    [ObservableProperty]
    private bool isSigningOut;

    /// <inheritdoc />
    public ImmutableArray<PostSummary> Images =>
        [.. Posts.Where(card => card.CanViewImage).Select(card => card.Summary)];

    /// <inheritdoc />
    public bool CanLoadMore => false;

    /// <inheritdoc />
    public Task LoadMoreAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public override Task LoadAsync() => RunAsync(async cancellationToken =>
    {
        PersonProfile found = await api.GetPersonAsync(personId, cancellationToken).ConfigureAwait(true);

        ClearContent();
        Profile = found;
        Bio = MarkdownParser.Parse(found.Bio);

        foreach (PostSummary summary in found.Posts)
        {
            var card = new PostCardViewModel(
                summary,
                Services.ImageLoader,
                Services.Now,
                settings.BlurNsfwImages,
                api,
                Services.Subscriptions,
                Navigator,
                OpenPost,
                ViewImage);

            Posts.Add(card);
            _ = card.LoadThumbnailAsync();
        }

        foreach (CommentNode node in found.Comments)
        {
            Comments.Add(new ProfileCommentViewModel(node, Services.Now, OpenCommentPostAsync));
        }

        HasNothing = Posts.Count == 0 && Comments.Count == 0;
    });

    /// <summary>
    /// Ends the session. The page goes with it: what it shows belongs to an account that is no
    /// longer signed in, and its own sign-out button would be left offering to do it again.
    /// </summary>
    [RelayCommand]
    private async Task SignOutAsync()
    {
        if (signOut is null || IsSigningOut)
        {
            return;
        }

        IsSigningOut = true;

        try
        {
            await signOut().ConfigureAwait(true);
        }
        finally
        {
            IsSigningOut = false;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearContent();
        }

        base.Dispose(disposing);
    }

    private void ClearContent()
    {
        foreach (PostCardViewModel card in Posts)
        {
            card.Dispose();
        }

        Posts.Clear();
        Comments.Clear();
    }

    private void ViewImage(PostCardViewModel card) => Navigator.ShowImage(card.Summary, this);

    private void OpenPost(PostCardViewModel card) =>
        Navigator.Push(new PostDetailViewModel(Services, Navigator, api, card.Summary, settings));

    /// <summary>
    /// Opens the post a comment was written on. The comment carries only the post's identifier, so
    /// the post itself has to be fetched before there is a page to show.
    /// </summary>
    private async Task OpenCommentPostAsync(PostId postId)
    {
        try
        {
            PostSummary summary = await api.GetPostAsync(postId, Lifetime).ConfigureAwait(true);
            Navigator.Push(new PostDetailViewModel(Services, Navigator, api, summary, settings));
        }
        catch (OperationCanceledException)
        {
            // The page went away.
        }
        catch (LemmyApiException exception)
        {
            ErrorMessage = exception.Message;
        }
    }
}
