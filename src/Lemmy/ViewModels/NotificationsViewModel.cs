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
/// What people have said to the signed-in account: replies to their posts and comments, and
/// comments that named them. Lemmy keeps those in two lists and the client merges them, so what
/// arrives here is already one list in time order.
/// </summary>
public sealed partial class NotificationsViewModel : PageViewModel
{
    private readonly ILemmyApi api;
    private readonly AppSettings settings;

    /// <summary>Opens the notification list.</summary>
    /// <param name="services">The app's services.</param>
    /// <param name="navigator">Where a tapped notification leads.</param>
    /// <param name="api">The instance to read.</param>
    /// <param name="settings">The reader's saved preferences, for the post the list opens.</param>
    public NotificationsViewModel(
        AppServices services,
        INavigator navigator,
        ILemmyApi api,
        AppSettings settings)
        : base(services, navigator)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(settings);

        this.api = api;
        this.settings = settings;
    }

    /// <inheritdoc />
    public override string Title => "Notifications";

    /// <summary>The replies and mentions, newest first.</summary>
    public ObservableCollection<NotificationViewModel> Notifications { get; } = [];

    /// <summary>Whether to leave out what has already been seen.</summary>
    [ObservableProperty]
    private bool unreadOnly = true;

    /// <summary>What the filter control says.</summary>
    public string FilterLabel => UnreadOnly ? "Showing unread" : "Showing everything";

    /// <summary>Whether the list came back with nothing in it.</summary>
    [ObservableProperty]
    private bool hasNothing;

    /// <summary>What that says, which depends on whether anything was filtered out.</summary>
    public string NothingLabel => UnreadOnly
        ? "Nothing unread. Everything people have sent you has been seen."
        : "Nobody has replied to you or mentioned you yet.";

    /// <summary>Set while everything is being marked read.</summary>
    [ObservableProperty]
    private bool isMarkingAll;

    /// <summary>Why the last mark-everything-read failed, or <see langword="null"/>.</summary>
    [ObservableProperty]
    private string? actionError;

    /// <summary>Whether there is anything on screen that marking everything read would change.</summary>
    public bool CanMarkAllRead => Notifications.Any(row => row.IsUnread);

    /// <inheritdoc />
    public override Task LoadAsync() => RunAsync(async cancellationToken =>
    {
        var query = new NotificationQuery(
            CommentSortType.New,
            1,
            PageSize.Clamp(PageSize.Maximum),
            UnreadOnly);

        ImmutableArray<Notification> fetched = await api
            .GetNotificationsAsync(query, cancellationToken)
            .ConfigureAwait(true);

        // Fetched before anything is discarded, as everywhere else that reloads: a refresh that
        // fails costs the reader the update rather than the list.
        Notifications.Clear();
        foreach (Notification notification in fetched)
        {
            Notifications.Add(new NotificationViewModel(notification, Services.Now, OpenAsync, SetReadAsync));
        }

        HasNothing = Notifications.Count == 0;

        // The badge follows the server's own count, not the rows on screen: this is one page of a
        // possibly longer list, and counting what arrived would shrink a badge that is telling the
        // truth. Everything after this adjusts that number rather than recounting it.
        Services.Unread.Set(await api.GetUnreadCountAsync(cancellationToken).ConfigureAwait(true));
        OnPropertyChanged(nameof(CanMarkAllRead));
    });

    /// <summary>Turns the unread filter on or off and fetches again.</summary>
    [RelayCommand]
    private Task ToggleFilter()
    {
        UnreadOnly = !UnreadOnly;
        return LoadAsync();
    }

    /// <summary>Marks everything waiting as read, which the server does in one request.</summary>
    [RelayCommand]
    private async Task MarkAllReadAsync(CancellationToken cancellationToken)
    {
        IsMarkingAll = true;
        ActionError = null;

        try
        {
            await api.MarkEverythingReadAsync(cancellationToken).ConfigureAwait(true);

            foreach (NotificationViewModel row in Notifications)
            {
                row.IsRead = true;
            }

            // The rows on screen stay where they are even under the unread filter. Emptying the
            // list under the reader's finger the moment they press it would take away the very
            // thing they just acted on, with no way to see what it was.
            Services.Unread.Clear();
            OnPropertyChanged(nameof(CanMarkAllRead));
        }
        catch (OperationCanceledException)
        {
            // The page went away.
        }
        catch (LemmyApiException exception)
        {
            ActionError = exception.Message;
        }
        finally
        {
            IsMarkingAll = false;
        }
    }

    /// <summary>
    /// Opens the post a notification was written on. The notification is marked read on the way,
    /// because opening it is what seeing it means — but a failure to record that must not stop the
    /// reader getting to the post.
    /// </summary>
    private async Task OpenAsync(Notification notification)
    {
        NotificationViewModel? row = Notifications.FirstOrDefault(
            candidate => candidate.Notification.Kind == notification.Kind
                && candidate.Notification.Id == notification.Id);

        if (row is { IsUnread: true })
        {
            await SetReadAsync(row, true).ConfigureAwait(true);
        }

        await ShowPostAsync(notification.PostId).ConfigureAwait(true);
    }

    private async Task ShowPostAsync(PostId postId)
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

    /// <summary>
    /// Changes one notification's read state. The row is moved first and put back if the server
    /// refuses, the same bargain the vote arrows make: waiting for a round trip before a checkmark
    /// moves feels broken on a phone.
    /// </summary>
    private async Task SetReadAsync(NotificationViewModel row, bool read)
    {
        bool previous = row.IsRead;

        row.IsRead = read;
        row.ActionError = null;
        Adjust(row.Notification.Kind, read ? -1 : 1);

        try
        {
            await api
                .SetNotificationReadAsync(row.Notification.Kind, row.Notification.Id, read, Lifetime)
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The page went away.
        }
        catch (LemmyApiException exception)
        {
            row.IsRead = previous;
            row.ActionError = exception.Message;
            Adjust(row.Notification.Kind, read ? 1 : -1);
        }
    }

    /// <summary>Moves the badge by one, on whichever of the two counts this notification belongs to.</summary>
    private void Adjust(NotificationKind kind, int delta)
    {
        if (kind == NotificationKind.Reply)
        {
            Services.Unread.Adjust(delta, 0);
        }
        else
        {
            Services.Unread.Adjust(0, delta);
        }

        OnPropertyChanged(nameof(CanMarkAllRead));
    }

    partial void OnUnreadOnlyChanged(bool value)
    {
        _ = value;
        OnPropertyChanged(nameof(FilterLabel));
        OnPropertyChanged(nameof(NothingLabel));
    }
}
