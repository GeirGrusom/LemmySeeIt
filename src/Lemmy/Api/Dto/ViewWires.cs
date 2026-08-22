namespace Lemmy.Api.Dto;

/// <summary>A post together with the rows Lemmy joins onto it.</summary>
internal sealed record PostViewWire
{
    public PostWire? Post { get; init; }

    /// <summary>How the signed-in account voted. Absent entirely when nobody is signed in.</summary>
    public int? MyVote { get; init; }

    /// <summary>Whether the signed-in account follows the community.</summary>
    public string? Subscribed { get; init; }

    public PersonWire? Creator { get; init; }

    public CommunityWire? Community { get; init; }

    public PostAggregatesWire? Counts { get; init; }

    public bool CreatorIsModerator { get; init; }

    public bool CreatorIsAdmin { get; init; }
}

/// <summary>A comment together with the rows Lemmy joins onto it.</summary>
internal sealed record CommentViewWire
{
    public CommentWire? Comment { get; init; }

    /// <summary>How the signed-in account voted. Absent entirely when nobody is signed in.</summary>
    public int? MyVote { get; init; }

    public PersonWire? Creator { get; init; }

    public PostWire? Post { get; init; }

    public CommunityWire? Community { get; init; }

    public CommentAggregatesWire? Counts { get; init; }

    public bool CreatorIsModerator { get; init; }

    public bool CreatorIsAdmin { get; init; }
}

/// <summary>A community together with its counts.</summary>
internal sealed record CommunityViewWire
{
    public CommunityWire? Community { get; init; }

    /// <summary>"Subscribed", "NotSubscribed" or "Pending"; absent when nobody is signed in.</summary>
    public string? Subscribed { get; init; }

    public CommunityAggregatesWire? Counts { get; init; }
}

/// <summary>An instance's own record together with its counts.</summary>
internal sealed record SiteViewWire
{
    public SiteWire? Site { get; init; }

    public SiteAggregatesWire? Counts { get; init; }

    public LocalSiteWire? LocalSite { get; init; }
}

/// <summary>
/// The instance's own policy, which only its own server reports. Lemmy carries a couple of dozen
/// settings here; these are the ones that decide whether signing up is worth offering.
/// </summary>
internal sealed record LocalSiteWire
{
    /// <summary>"Open", "RequireApplication" or "Closed".</summary>
    public string? RegistrationMode { get; init; }

    /// <summary>What the instance asks applicants, when it asks anything.</summary>
    public string? ApplicationQuestion { get; init; }

    public bool RequireEmailVerification { get; init; }
}

/// <summary>An instance's own record as Lemmy's API v3 sends it.</summary>
internal sealed record SiteWire
{
    public int Id { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    public string? Sidebar { get; init; }

    public string? Icon { get; init; }

    public string? Banner { get; init; }

    public string? ActorId { get; init; }
}
