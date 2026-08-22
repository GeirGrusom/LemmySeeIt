namespace Lemmy.Domain.Models;

/// <summary>A community — Lemmy's equivalent of a subreddit.</summary>
/// <param name="Id">The instance-local identifier.</param>
/// <param name="Name">The short name used in URLs and mentions.</param>
/// <param name="Title">The human-readable title.</param>
/// <param name="Description">The sidebar text.</param>
/// <param name="ActorId">The fediverse-wide identity.</param>
/// <param name="Icon">The community icon, when there is one.</param>
/// <param name="Banner">The community banner, when there is one.</param>
/// <param name="IsLocal">Whether the community is hosted on the instance being read.</param>
/// <param name="IsNsfw">Whether the community is flagged as not safe for work.</param>
/// <param name="IsRemoved">Whether an admin has removed the community.</param>
/// <param name="IsDeleted">Whether the owner has deleted the community.</param>
/// <param name="Published">When the community was created.</param>
public sealed record Community(
    CommunityId Id,
    CommunityName Name,
    string Title,
    MarkdownText Description,
    ActorId ActorId,
    WebLink? Icon,
    WebLink? Banner,
    bool IsLocal,
    bool IsNsfw,
    bool IsRemoved,
    bool IsDeleted,
    DateTimeOffset Published)
{
    /// <summary>The unambiguous <c>!name@instance</c> form.</summary>
    public string QualifiedName =>
        ActorId.Instance is { } instance ? $"!{Name.Value}@{instance.Value}" : $"!{Name.Value}";

    /// <summary>What to show as a heading: the title when set, otherwise the short name.</summary>
    public string PreferredTitle => string.IsNullOrWhiteSpace(Title) ? Name.Value : Title;
}
