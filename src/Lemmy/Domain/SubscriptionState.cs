namespace Lemmy.Domain;

/// <summary>Where the signed-in account stands with a community.</summary>
public enum SubscriptionState
{
    /// <summary>Not following, or nobody is signed in.</summary>
    NotSubscribed,

    /// <summary>Following: the community's posts appear in the Subscribed feed.</summary>
    Subscribed,

    /// <summary>
    /// A follow the community has not acted on yet. Communities can require approval, and a remote
    /// one has to answer over federation before the follow is real, which is not instant.
    /// </summary>
    Pending,
}

/// <summary>Reads the subscription state Lemmy sends as a string.</summary>
public static class SubscriptionStateExtensions
{
    /// <summary>Maps the wire value; anything unrecognised counts as not following.</summary>
    public static SubscriptionState ToSubscriptionState(this string? value) => value switch
    {
        "Subscribed" => SubscriptionState.Subscribed,
        "Pending" => SubscriptionState.Pending,
        _ => SubscriptionState.NotSubscribed,
    };

    /// <summary>
    /// Whether the account has asked to follow, whether or not the community has agreed yet. This is
    /// what decides which way the button acts: pressing it while pending unsubscribes.
    /// </summary>
    public static bool IsFollowing(this SubscriptionState state) =>
        state is SubscriptionState.Subscribed or SubscriptionState.Pending;
}
