using CommunityToolkit.Mvvm.Input;
using Lemmy.Domain.Models;

namespace Lemmy.ViewModels;

/// <summary>One row in the community directory.</summary>
public sealed partial class CommunityRowViewModel : ViewModelBase
{
    private readonly Action<CommunityRowViewModel> openRequested;

    /// <summary>Wraps a community for display.</summary>
    public CommunityRowViewModel(CommunitySummary summary, Action<CommunityRowViewModel> openRequested)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(openRequested);

        Summary = summary;
        this.openRequested = openRequested;
    }

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
}
