using CommunityToolkit.Mvvm.Input;
using Lemmy.Api;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>One row in the community directory.</summary>
public sealed partial class CommunityRowViewModel : ViewModelBase, IDisposable
{
    private readonly Action<CommunityRowViewModel> openRequested;

    /// <summary>Wraps a community for display.</summary>
    /// <param name="summary">The community and its counts.</param>
    /// <param name="api">The client the row subscribes through.</param>
    /// <param name="subscriptions">Where subscription changes are shared with everything else on screen.</param>
    /// <param name="openRequested">Called when the reader opens the community.</param>
    public CommunityRowViewModel(
        CommunitySummary summary,
        ILemmyApi api,
        SubscriptionTracker subscriptions,
        Action<CommunityRowViewModel> openRequested)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentNullException.ThrowIfNull(openRequested);

        Summary = summary;
        this.openRequested = openRequested;

        Subscription = new SubscribeButtonViewModel(
            summary.Community.Id,
            summary.Subscription,
            api.IsAuthenticated,
            subscriptions,
            (follow, token) => api.SetSubscriptionAsync(summary.Community.Id, follow, token));
    }

    /// <summary>The subscribe control for this community.</summary>
    public SubscribeButtonViewModel Subscription { get; }

    /// <summary>The community and its counts.</summary>
    public CommunitySummary Summary { get; }

    /// <summary>The community's title.</summary>
    public string Title => Summary.Community.PreferredTitle;

    /// <summary>The community, in <c>!name@instance</c> form.</summary>
    public string QualifiedName => Summary.Community.QualifiedName;

    /// <summary>A one-line flattening of the sidebar.</summary>
    public string DescriptionPreview => Summary.Community.Description.ToPreview(160);

    /// <summary>Whether there is a sidebar to preview; plenty of communities have none.</summary>
    public bool HasDescription => !Summary.Community.Description.IsEmpty;

    /// <summary>Subscribers and monthly actives, shortened for a badge.</summary>
    public string ActivityLabel =>
        $"{Summary.Tally.Subscribers.ToCompactString()} subs · {Summary.Tally.UsersActiveMonth.ToCompactString()}/mo";

    /// <summary>Opens the community's feed.</summary>
    [RelayCommand]
    private void Open() => openRequested(this);

    /// <inheritdoc />
    public void Dispose() => Subscription.Dispose();
}
