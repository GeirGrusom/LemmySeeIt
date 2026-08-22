using System.Collections.Immutable;
using Lemmy.Domain;

namespace Lemmy.Services;

/// <summary>
/// A starting set of servers to offer somebody who has not picked one, ordered roughly by how busy
/// they are.
/// </summary>
/// <remarks>
/// Baked in rather than fetched. The lists that rank Lemmy instances live on third-party
/// aggregators, and reaching for one every time the server picker opens would add a dependency on
/// somebody else's uptime, tell them the app is running, and leave the picker empty offline — all
/// to populate a list the reader overwrites the moment they choose anything. The cost is that this
/// goes stale: it was taken from lemmyverse.net's rankings and every entry answered
/// <c>/api/v3/site</c> when it was written, but a server can close or fade, and none of that shows
/// here until someone refreshes the list.
/// </remarks>
public static class KnownInstances
{
    /// <summary>The suggestions, most active first.</summary>
    public static ImmutableArray<InstanceAddress> Suggested { get; } =
    [
        InstanceAddress.Parse("lemmy.world"),
        InstanceAddress.Parse("sh.itjust.works"),
        InstanceAddress.Parse("lemmy.zip"),
        InstanceAddress.Parse("lemmy.ml"),
        InstanceAddress.Parse("lemmy.dbzer0.com"),
        InstanceAddress.Parse("lemmy.ca"),
        InstanceAddress.Parse("feddit.org"),
        InstanceAddress.Parse("programming.dev"),
        InstanceAddress.Parse("lemmy.blahaj.zone"),
        InstanceAddress.Parse("discuss.tchncs.de"),
        InstanceAddress.Parse("sopuli.xyz"),
        InstanceAddress.Parse("slrpnk.net"),
    ];
}
