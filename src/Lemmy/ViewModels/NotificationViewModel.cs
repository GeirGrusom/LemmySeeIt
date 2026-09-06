using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Domain.Markdown;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>
/// One row in the notification list: what somebody wrote to you, and the way back to the post they
/// wrote it on.
/// </summary>
/// <remarks>
/// Deliberately not <see cref="CommentViewModel"/>, for the same reason a comment on an account's
/// page is not one either: this is a comment taken out of its thread, with no replies under it and
/// nothing to fold. What it has instead is a read state, which no other view of a comment has.
/// </remarks>
public sealed partial class NotificationViewModel : ViewModelBase
{
    private readonly Func<Notification, Task> open;
    private readonly Func<NotificationViewModel, bool, Task> setRead;

    /// <summary>Wraps a notification for the list.</summary>
    /// <param name="notification">The reply or mention.</param>
    /// <param name="now">The current time, for the age label.</param>
    /// <param name="open">Opens the post it was written on.</param>
    /// <param name="setRead">Marks it read, or puts it back to unread.</param>
    public NotificationViewModel(
        Notification notification,
        DateTimeOffset now,
        Func<Notification, Task> open,
        Func<NotificationViewModel, bool, Task> setRead)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(setRead);

        Notification = notification;
        this.open = open;
        this.setRead = setRead;

        isRead = notification.IsRead;
        AgeLabel = RelativeTime.Format(notification.Received, now);
        Content = MarkdownParser.Parse(notification.Comment.VisibleContent);
    }

    /// <summary>The notification itself.</summary>
    public Notification Notification { get; }

    /// <summary>What was written, parsed into blocks.</summary>
    public ImmutableArray<MarkdownBlock> Content { get; }

    /// <summary>How long ago it arrived.</summary>
    public string AgeLabel { get; }

    /// <summary>Who wrote it.</summary>
    public string AuthorLabel => Notification.Creator.PreferredName;

    /// <summary>The post it was written on.</summary>
    public string PostLabel => Notification.Post.Title.Value;

    /// <summary>The community that post lives in.</summary>
    public string CommunityLabel => Notification.Community.QualifiedName;

    /// <summary>
    /// What kind of notification this is, said plainly. A mention is worth distinguishing: it can
    /// arrive on a post the reader has nothing to do with, so "replied" would be a lie.
    /// </summary>
    public string KindLabel => Notification.Kind == NotificationKind.Mention ? "mentioned you" : "replied";

    /// <summary>Whether it has been seen. Set here first, so the row answers the tap immediately.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnread))]
    [NotifyPropertyChangedFor(nameof(ToggleReadLabel))]
    private bool isRead;

    /// <summary>Whether it still counts towards the badge.</summary>
    public bool IsUnread => !IsRead;

    /// <summary>What the control that changes it says.</summary>
    public string ToggleReadLabel => IsRead ? "Mark unread" : "Mark read";

    /// <summary>Why the last attempt to change the read state failed, or <see langword="null"/>.</summary>
    [ObservableProperty]
    private string? actionError;

    /// <summary>Opens the post this was written on.</summary>
    [RelayCommand]
    private Task Open() => open(Notification);

    /// <summary>Marks it read, or puts it back to unread.</summary>
    [RelayCommand]
    private Task ToggleRead() => setRead(this, !IsRead);
}
