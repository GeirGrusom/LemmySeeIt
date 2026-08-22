using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>
/// The shell: which instance is being read, which section is selected, and the back stack within
/// that section. Each section keeps its own root page, so switching sections and coming back does
/// not re-fetch a feed the reader has already scrolled.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase, INavigator, IDisposable
{
    private readonly AppServices services;
    private readonly Stack<PageViewModel> backStack = new();
    private readonly Dictionary<AppSection, PageViewModel> sectionRoots = [];

    private ILemmyApi api;
    private AppSettings settings;
    private SessionToken session;

    /// <summary>Suppresses saving and reloading while settings are being applied rather than chosen.</summary>
    private bool isApplyingSettings;
    private bool isDisposed;

    /// <summary>Creates the shell over the app's services.</summary>
    public MainViewModel(AppServices services)
        : this(services, AppSettings.Default)
    {
    }

    /// <summary>Creates the shell with settings already loaded, which is what the tests do.</summary>
    public MainViewModel(AppServices services, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        this.services = services;
        this.settings = settings;

        api = services.ApiFactory.Create(settings.Instance);
        instanceInput = settings.Instance.Value;
        instanceLabel = settings.Instance.Value;
        sessionNote = services.SessionStore.Description;
    }

    /// <summary>The page currently on screen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    private PageViewModel? currentPage;

    /// <summary>The heading for the current page.</summary>
    public string PageTitle => CurrentPage?.Title ?? string.Empty;

    /// <summary>The section whose root is at the bottom of the current back stack.</summary>
    [ObservableProperty]
    private AppSection selectedSection;

    /// <summary>The instance being read, for the header.</summary>
    [ObservableProperty]
    private string instanceLabel;

    /// <summary>What the reader has typed into the instance box.</summary>
    [ObservableProperty]
    private string instanceInput;

    /// <summary>The complaint shown when the typed instance is not usable.</summary>
    [ObservableProperty]
    private string? instanceError;

    /// <summary>Whether the instance picker is open.</summary>
    [ObservableProperty]
    private bool isInstancePickerOpen;

    /// <summary>The full-screen image on top of everything, or <see langword="null"/> when there is none.</summary>
    [ObservableProperty]
    private ImageViewerViewModel? imageViewer;

    /// <summary>Who is signed in on this instance, or <see langword="null"/> when nobody is.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn))]
    [NotifyPropertyChangedFor(nameof(AccountLabel))]
    [NotifyPropertyChangedFor(nameof(AccountQualifiedName))]
    private Account? account;

    /// <summary>Whether the sign-in sheet is open.</summary>
    [ObservableProperty]
    private bool isSignInOpen;

    /// <summary>Whether the account sheet is open.</summary>
    [ObservableProperty]
    private bool isAccountOpen;

    /// <summary>What the reader typed as their account name or email.</summary>
    [ObservableProperty]
    private string signInName = string.Empty;

    /// <summary>The password, held only until the request goes out.</summary>
    [ObservableProperty]
    private string signInPassword = string.Empty;

    /// <summary>The second factor, for accounts that have one.</summary>
    [ObservableProperty]
    private string signInTotp = string.Empty;

    /// <summary>What went wrong with the last attempt.</summary>
    [ObservableProperty]
    private string? signInError;

    /// <summary>Whether a sign-in request is in flight.</summary>
    [ObservableProperty]
    private bool isSigningIn;

    /// <summary>What this device does with the session, so the reader is told rather than guessing.</summary>
    [ObservableProperty]
    private string sessionNote = string.Empty;

    /// <summary>Whether to include content flagged not safe for work.</summary>
    [ObservableProperty]
    private bool showNsfw;

    /// <summary>Whether such images start covered, once they are shown at all.</summary>
    [ObservableProperty]
    private bool blurNsfwImages = true;

    /// <summary>Whether anyone is signed in.</summary>
    public bool IsSignedIn => Account is not null;

    /// <summary>The account name for the header, or an invitation to sign in.</summary>
    public string AccountLabel => Account?.PreferredName ?? "Sign in";

    /// <summary>The unambiguous account name, for the account sheet.</summary>
    public string AccountQualifiedName => Account?.QualifiedName ?? string.Empty;

    /// <summary>Whether there is a page to go back to.</summary>
    public bool CanPop => backStack.Count > 0;

    /// <summary>The sections offered in the navigation bar.</summary>
    public static ReadOnlyCollection<SectionOption> Sections => DisplayOptions.Sections;

    /// <summary>
    /// Loads settings, restores any session, and shows the feed. Called once at startup; failures
    /// fall back to defaults rather than blocking the app behind an error the reader cannot act on.
    /// </summary>
    public async Task InitialiseAsync()
    {
        settings = await services.SettingsStore.LoadAsync().ConfigureAwait(true);
        SessionNote = services.SessionStore.Description;
        ShowSettings(settings);

        await RestoreSessionAsync(settings.Instance).ConfigureAwait(true);

        ApplyInstance(settings.Instance);
        await ShowSectionAsync(AppSection.Feed).ConfigureAwait(true);
    }

    /// <summary>
    /// Picks up a stored session, if there is one for this instance and it still works.
    /// </summary>
    /// <remarks>
    /// Whether it still works has to be asked, not assumed: Lemmy answers a request carrying a dead
    /// token with a perfectly normal 200 that simply omits the account, so the only way to know is
    /// to look for the account and find it missing.
    /// </remarks>
    private async Task RestoreSessionAsync(InstanceAddress instance)
    {
        StoredSession? stored = await services.SessionStore.LoadAsync().ConfigureAwait(true);

        if (stored is not { IsValid: true } found || found.Instance != instance)
        {
            return;
        }

        ILemmyApi candidate = services.ApiFactory.Create(instance, found.Token);

        try
        {
            Account? whoami = await candidate.GetMyAccountAsync().ConfigureAwait(true);

            if (whoami is null)
            {
                await services.SessionStore.ClearAsync().ConfigureAwait(true);
                return;
            }

            session = found.Token;
            Account = whoami;

            await AdoptAccountPreferencesAsync(whoami).ConfigureAwait(true);
        }
        catch (LemmyApiException)
        {
            // Offline at launch is not a reason to throw the session away; it can be checked again
            // the next time the app starts with a working connection.
            session = found.Token;
            Account = new Account(default, found.AccountName, null, instance, null);
        }
    }

    /// <inheritdoc />
    public void ShowImage(PostSummary summary, IImageGallery gallery)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(gallery);

        if (summary.Post.FullImage is null)
        {
            return;
        }

        CloseImage();

        var viewer = new ImageViewerViewModel(gallery, summary, services.ImageLoader, CloseImage);
        ImageViewer = viewer;

        // Not awaited: the viewer opens immediately and shows its own progress while it downloads.
        _ = viewer.LoadAsync();
    }

    /// <summary>Dismisses the full-screen image, releasing the bitmap with it.</summary>
    public void CloseImage()
    {
        ImageViewerViewModel? viewer = ImageViewer;
        ImageViewer = null;
        viewer?.Dispose();
    }

    /// <inheritdoc />
    public void Push(PageViewModel page)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (CurrentPage is { } previous)
        {
            backStack.Push(previous);
        }

        SetPage(page);
        _ = page.LoadAsync();
    }

    /// <inheritdoc />
    public void Pop()
    {
        if (!backStack.TryPop(out PageViewModel? previous))
        {
            return;
        }

        PageViewModel? leaving = CurrentPage;
        SetPage(previous);

        // A section root stays alive for the reader to come back to; anything else is finished with.
        if (leaving is not null && !sectionRoots.ContainsValue(leaving))
        {
            leaving.Dispose();
        }
    }

    /// <summary>
    /// Handles a back request, reporting whether it did anything. Android's back gesture is the
    /// primary way people navigate there, and the order matters: inside a section, back unwinds the
    /// page stack; at a section root, it returns to the feed; at the feed, it does nothing and lets
    /// the system background the app, which is what Android users expect.
    /// </summary>
    public bool TryGoBack()
    {
        // The image sits over everything, so it is the first thing back should dismiss.
        if (ImageViewer is not null)
        {
            CloseImage();
            return true;
        }

        if (CanPop)
        {
            Pop();
            return true;
        }

        if (SelectedSection != AppSection.Feed)
        {
            SelectedSection = AppSection.Feed;
            return true;
        }

        return false;
    }

    /// <summary>Goes back one page.</summary>
    [RelayCommand]
    private void GoBack() => Pop();

    /// <summary>
    /// Opens the sign-in sheet, or the account sheet when someone already is signed in.
    /// </summary>
    /// <remarks>
    /// Deliberately does not sign out on its own. Signing out cannot be undone — it invalidates the
    /// token on the server, so getting back in means typing a password again — and the control sits
    /// in a header whose layout shifts as the account name replaces "Sign in". A single stray tap
    /// should not be able to end a session.
    /// </remarks>
    [RelayCommand]
    private void ToggleAccount()
    {
        if (IsSignedIn)
        {
            IsAccountOpen = true;
            return;
        }

        SignInError = null;
        SignInPassword = string.Empty;
        SignInTotp = string.Empty;
        IsSignInOpen = true;
    }

    /// <summary>Closes the account sheet without changing anything.</summary>
    [RelayCommand]
    private void CloseAccount() => IsAccountOpen = false;

    /// <summary>Signs out, from the account sheet where it has been asked for explicitly.</summary>
    [RelayCommand]
    private async Task ConfirmSignOutAsync()
    {
        IsAccountOpen = false;
        await SignOutAsync().ConfigureAwait(true);
    }

    /// <summary>Closes the sign-in sheet without signing in.</summary>
    [RelayCommand]
    private void CancelSignIn()
    {
        IsSignInOpen = false;
        SignInPassword = string.Empty;
        SignInTotp = string.Empty;
        SignInError = null;
    }

    /// <summary>Signs in to the current instance.</summary>
    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsSigningIn)
        {
            return;
        }

        var request = new LoginRequest(SignInName, SignInPassword, SignInTotp);
        if (!request.IsComplete)
        {
            SignInError = "Enter your account name and password.";
            return;
        }

        IsSigningIn = true;
        SignInError = null;

        try
        {
            // Signing in is the one call made without a session, so it uses its own client.
            ILemmyApi anonymous = services.ApiFactory.Create(settings.Instance);
            SessionToken token = await anonymous.LogInAsync(request).ConfigureAwait(true);

            ILemmyApi authenticated = services.ApiFactory.Create(settings.Instance, token);
            Account? whoami = await authenticated.GetMyAccountAsync().ConfigureAwait(true);

            if (whoami is null)
            {
                SignInError = $"{settings.Instance.Value} issued a token but would not say who it belongs to.";
                return;
            }

            session = token;
            Account = whoami;

            await AdoptAccountPreferencesAsync(whoami).ConfigureAwait(true);

            await services.SessionStore
                .SaveAsync(new StoredSession(settings.Instance, token, whoami.Name))
                .ConfigureAwait(true);

            IsSignInOpen = false;
            await ReopenSectionsAsync().ConfigureAwait(true);
        }
        catch (LemmyApiException exception)
        {
            SignInError = exception.Message;
        }
        finally
        {
            // Held no longer than the request itself.
            SignInPassword = string.Empty;
            SignInTotp = string.Empty;
            IsSigningIn = false;
        }
    }

    /// <summary>
    /// Signs out, telling the instance to invalidate the token rather than merely forgetting it —
    /// Lemmy tokens do not expire, so a token only forgotten locally stays usable indefinitely.
    /// </summary>
    public async Task SignOutAsync()
    {
        ILemmyApi signedIn = api;

        session = default;
        Account = null;

        await services.SessionStore.ClearAsync().ConfigureAwait(true);
        await signedIn.LogOutAsync().ConfigureAwait(true);
        await ReopenSectionsAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Takes on the content settings the account already has on its instance, so that someone who
    /// turned these on through the web does not have to find them again here.
    /// </summary>
    private async Task AdoptAccountPreferencesAsync(Account whoami)
    {
        AppSettings updated = settings with { ShowNsfw = whoami.ShowNsfw, BlurNsfwImages = whoami.BlurNsfw };

        if (updated == settings)
        {
            return;
        }

        settings = updated;
        ShowSettings(settings);

        await services.SettingsStore.SaveAsync(settings).ConfigureAwait(true);
    }

    /// <summary>Ends the session on the instance being left behind, without rebuilding anything.</summary>
    private async Task SignOutOfCurrentInstanceAsync()
    {
        ILemmyApi signedIn = api;

        session = default;
        Account = null;

        await services.SessionStore.ClearAsync().ConfigureAwait(true);
        await signedIn.LogOutAsync().ConfigureAwait(true);
    }

    /// <summary>Rebuilds every section against the current session, since what they show depends on it.</summary>
    private async Task ReopenSectionsAsync()
    {
        ApplyInstance(settings.Instance);
        await ShowSectionAsync(AppSection.Feed).ConfigureAwait(true);
    }

    /// <summary>Opens the licences and attribution page.</summary>
    [RelayCommand]
    private void ShowAttribution()
    {
        IsInstancePickerOpen = false;
        Push(new AttributionViewModel(services, this));
    }

    /// <summary>Opens or closes the instance picker.</summary>
    [RelayCommand]
    private void ToggleInstancePicker()
    {
        IsInstancePickerOpen = !IsInstancePickerOpen;
        InstanceError = null;
        InstanceInput = InstanceLabel;
    }

    /// <summary>
    /// Points the app at a different server. Everything loaded so far is instance-local, so every
    /// page is torn down and the sections start again from the new instance's front page.
    /// </summary>
    [RelayCommand]
    private async Task ApplyInstanceAsync()
    {
        if (!InstanceAddress.TryParse(InstanceInput.AsSpan(), out InstanceAddress address))
        {
            InstanceError = "That does not look like a server address. Try something like lemmy.world.";
            return;
        }

        if (address == settings.Instance)
        {
            IsInstancePickerOpen = false;
            return;
        }

        InstanceError = null;

        // A token belongs to one server. Moving to another is signing out of the first.
        if (IsSignedIn)
        {
            await SignOutOfCurrentInstanceAsync().ConfigureAwait(true);
        }

        settings = settings with { Instance = address };
        ApplyInstance(address);
        IsInstancePickerOpen = false;

        await services.SettingsStore.SaveAsync(settings).ConfigureAwait(true);
        await ShowSectionAsync(AppSection.Feed).ConfigureAwait(true);
    }

    /// <summary>Switches to a section, creating its root page the first time.</summary>
    public async Task ShowSectionAsync(AppSection section)
    {
        ClearBackStack();

        if (!sectionRoots.TryGetValue(section, out PageViewModel? root))
        {
            root = CreateRoot(section);
            sectionRoots[section] = root;
            SetPage(root);
            await root.LoadAsync().ConfigureAwait(true);
            return;
        }

        SetPage(root);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        CloseImage();
        ClearBackStack();
        foreach (PageViewModel root in sectionRoots.Values)
        {
            root.Dispose();
        }

        sectionRoots.Clear();
        CurrentPage = null;
    }

    partial void OnSelectedSectionChanged(AppSection value) => _ = ShowSectionAsync(value);

    /// <summary>Pushes the saved settings into the toggles without taking that for a choice.</summary>
    private void ShowSettings(AppSettings current)
    {
        isApplyingSettings = true;

        try
        {
            ShowNsfw = current.ShowNsfw;
            BlurNsfwImages = current.BlurNsfwImages;
        }
        finally
        {
            isApplyingSettings = false;
        }
    }

    partial void OnShowNsfwChanged(bool value) => _ = ApplyContentSettingAsync(settings with { ShowNsfw = value });

    partial void OnBlurNsfwImagesChanged(bool value) => _ = ApplyContentSettingAsync(settings with { BlurNsfwImages = value });

    /// <summary>
    /// Saves a content setting and rebuilds the sections against it. A rebuild rather than a
    /// refresh: what the server is asked for changes, so the pages have to be asked again from the
    /// start rather than filtered after the fact.
    /// </summary>
    private async Task ApplyContentSettingAsync(AppSettings updated)
    {
        if (isApplyingSettings || isDisposed || updated == settings)
        {
            return;
        }

        settings = updated;

        await services.SettingsStore.SaveAsync(settings).ConfigureAwait(true);
        await ReopenSectionsAsync().ConfigureAwait(true);
    }

    private PageViewModel CreateRoot(AppSection section) => section switch
    {
        AppSection.Communities => new CommunitiesViewModel(services, this, api, settings),
        AppSection.Search => new SearchViewModel(services, this, api, settings),
        _ => new FeedViewModel(services, this, api, settings),
    };

    private void ApplyInstance(InstanceAddress address)
    {
        // Nothing from the old server survives the switch, an open picture included.
        CloseImage();

        api = services.ApiFactory.Create(address, session);
        InstanceLabel = address.Value;
        InstanceInput = address.Value;

        ClearBackStack();
        foreach (PageViewModel root in sectionRoots.Values)
        {
            root.Dispose();
        }

        sectionRoots.Clear();
    }

    private void ClearBackStack()
    {
        while (backStack.TryPop(out PageViewModel? page))
        {
            if (!sectionRoots.ContainsValue(page))
            {
                page.Dispose();
            }
        }

        OnPropertyChanged(nameof(CanPop));
    }

    private void SetPage(PageViewModel page)
    {
        CurrentPage = page;
        OnPropertyChanged(nameof(CanPop));
    }
}
