using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>
/// The subscribe control beside a community. Shared by the directory row, a community's own feed
/// header and the post page, all of which can show the same community at the same time.
/// </summary>
/// <remarks>
/// State is read through <see cref="SubscriptionTracker"/> rather than held here, so that
/// subscribing in one place is reflected everywhere else showing that community without a refetch.
/// </remarks>
public sealed partial class SubscribeButtonViewModel : ViewModelBase, IDisposable
{
    private readonly CommunityId community;
    private readonly SubscriptionState asFetched;
    private readonly SubscriptionTracker tracker;
    private readonly Func<bool, CancellationToken, Task<SubscriptionState>> send;

    private bool isDisposed;

    /// <summary>Creates the control over a community as some response described it.</summary>
    /// <param name="community">Which community this acts on.</param>
    /// <param name="asFetched">The state the response carrying that community reported.</param>
    /// <param name="canSubscribe">Whether anybody is signed in; the control is hidden when nobody is.</param>
    /// <param name="tracker">Where subscription changes are shared between places showing this community.</param>
    /// <param name="send">Performs the follow or unfollow.</param>
    public SubscribeButtonViewModel(
        CommunityId community,
        SubscriptionState asFetched,
        bool canSubscribe,
        SubscriptionTracker tracker,
        Func<bool, CancellationToken, Task<SubscriptionState>> send)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(send);

        this.community = community;
        this.asFetched = asFetched;
        this.tracker = tracker;
        this.send = send;

        CanSubscribe = canSubscribe;
        tracker.Changed += OnTrackerChanged;
    }

    /// <summary>Whether to offer the control at all.</summary>
    public bool CanSubscribe { get; }

    /// <summary>Where the account stands with the community right now.</summary>
    public SubscriptionState State => tracker.StateFor(community, asFetched);

    /// <summary>Whether the account has asked to follow, acknowledged or not.</summary>
    public bool IsFollowing => State.IsFollowing();

    /// <summary>Whether the follow is waiting on the community's own instance.</summary>
    public bool IsPending => State == SubscriptionState.Pending;

    /// <summary>What the button says.</summary>
    public string Label => State switch
    {
        SubscriptionState.Subscribed => "Subscribed",
        SubscriptionState.Pending => "Pending",
        _ => "Subscribe",
    };

    /// <summary>Set while a change is in flight.</summary>
    [ObservableProperty]
    private bool isBusy;

    /// <summary>Why the last change did not take, or <see langword="null"/>.</summary>
    [ObservableProperty]
    private string? errorMessage;

    /// <summary>Follows the community, or unfollows one already followed.</summary>
    [RelayCommand(CanExecute = nameof(CanToggle))]
    private async Task ToggleAsync(CancellationToken cancellationToken)
    {
        SubscriptionState before = State;
        bool follow = !before.IsFollowing();

        // Show the change now. Following a remote community can take a federation round trip to
        // become real, so waiting for the answer would leave the button dead for a noticeable beat.
        tracker.Record(community, follow ? SubscriptionState.Pending : SubscriptionState.NotSubscribed);
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            tracker.Record(community, await send(follow, cancellationToken).ConfigureAwait(true));
        }
        catch (OperationCanceledException)
        {
            // The page went away; what is on screen is about to be discarded with it.
        }
        catch (LemmyApiException exception)
        {
            tracker.Record(community, before);
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanToggle() => CanSubscribe && !IsBusy;

    private void OnTrackerChanged(object? sender, CommunityId changed)
    {
        if (changed != community || isDisposed)
        {
            return;
        }

        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsFollowing));
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(Label));
    }

    partial void OnIsBusyChanged(bool value) => ToggleCommand.NotifyCanExecuteChanged();

    /// <summary>Detaches from the tracker; rows are created and thrown away as the reader scrolls.</summary>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        tracker.Changed -= OnTrackerChanged;
    }
}
