using Lemmy.Domain;

namespace Lemmy.Services;

/// <summary>
/// What the app knows about the account's subscriptions, over and above what each response happened
/// to say.
/// </summary>
/// <remarks>
/// A community appears in many places at once — a dozen feed rows, the directory, the post it was
/// opened from — and every one of them was told the subscription state by the response that carried
/// it. Subscribing in one of those places would leave all the others claiming the opposite until
/// something refetched. This holds the change so that everything showing that community agrees
/// immediately, and it is emptied when the instance changes or the session ends, because a
/// subscription belongs to one account on one server.
/// </remarks>
public sealed class SubscriptionTracker
{
    private readonly Dictionary<CommunityId, SubscriptionState> known = [];

    /// <summary>Raised when a community's state changes, with the community that changed.</summary>
    public event EventHandler<CommunityId>? Changed;

    /// <summary>
    /// The state to show for a community: what the app was last told by a write, falling back to
    /// <paramref name="asFetched"/> — what the response that carried this community said.
    /// </summary>
    public SubscriptionState StateFor(CommunityId community, SubscriptionState asFetched) =>
        known.TryGetValue(community, out SubscriptionState state) ? state : asFetched;

    /// <summary>Records where a community now stands and tells anything showing it.</summary>
    public void Record(CommunityId community, SubscriptionState state)
    {
        if (known.TryGetValue(community, out SubscriptionState existing) && existing == state)
        {
            return;
        }

        known[community] = state;
        Changed?.Invoke(this, community);
    }

    /// <summary>
    /// Forgets everything. Called when the instance changes or the session ends: these identifiers
    /// are instance-local, so carrying them across would attach one server's answers to another's
    /// communities.
    /// </summary>
    public void Clear()
    {
        if (known.Count == 0)
        {
            return;
        }

        CommunityId[] forgotten = [.. known.Keys];
        known.Clear();

        foreach (CommunityId community in forgotten)
        {
            Changed?.Invoke(this, community);
        }
    }
}
