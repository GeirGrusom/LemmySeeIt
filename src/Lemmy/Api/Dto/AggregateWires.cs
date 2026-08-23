namespace Lemmy.Api.Dto;

/// <summary>A post's vote and comment counts as Lemmy's API v3 sends them.</summary>
internal sealed record PostAggregatesWire
{
    public int PostId { get; init; }

    public int Comments { get; init; }

    public int Score { get; init; }

    public int Upvotes { get; init; }

    public int Downvotes { get; init; }

    public DateTimeOffset? NewestCommentTime { get; init; }
}

/// <summary>A comment's vote and reply counts as Lemmy's API v3 sends them.</summary>
internal sealed record CommentAggregatesWire
{
    public int CommentId { get; init; }

    public int Score { get; init; }

    public int Upvotes { get; init; }

    public int Downvotes { get; init; }

    public int ChildCount { get; init; }
}

/// <summary>A community's activity counts as Lemmy's API v3 sends them.</summary>
internal sealed record CommunityAggregatesWire
{
    public int CommunityId { get; init; }

    public int Subscribers { get; init; }

    public int Posts { get; init; }

    public int Comments { get; init; }

    public int UsersActiveMonth { get; init; }
}

/// <summary>An instance's totals as Lemmy's API v3 sends them.</summary>
/// <summary>How much an account has written.</summary>
internal sealed record PersonAggregatesWire
{
    public int PostCount { get; init; }

    public int CommentCount { get; init; }
}

internal sealed record SiteAggregatesWire
{
    public int Users { get; init; }

    public int Posts { get; init; }

    public int Comments { get; init; }

    public int Communities { get; init; }
}
