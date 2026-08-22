namespace Lemmy.Domain.Models;

/// <summary>Which slice of the instance a feed draws from.</summary>
public enum ListingType
{
    /// <summary>Everything the instance can see, including federated communities.</summary>
    All,

    /// <summary>Only communities hosted on this instance.</summary>
    Local,

    /// <summary>Only communities the signed-in account subscribes to.</summary>
    Subscribed,

    /// <summary>Only communities the signed-in account moderates.</summary>
    ModeratorView,
}
