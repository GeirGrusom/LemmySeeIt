namespace Lemmy.Domain.Models;

/// <summary>A post, stripped of the vote tallies and viewer-specific flags that travel with it.</summary>
/// <param name="Id">The instance-local identifier.</param>
/// <param name="Title">The headline.</param>
/// <param name="Body">The Markdown body; empty for a bare link post.</param>
/// <param name="Url">The link a link post points at.</param>
/// <param name="Thumbnail">The server-generated preview image.</param>
/// <param name="UrlMediaType">What the server found at <paramref name="Url"/>, when it looked.</param>
/// <param name="EmbedTitle">The link's own title, as scraped by the server.</param>
/// <param name="EmbedDescription">The link's own description, as scraped by the server.</param>
/// <param name="CreatorId">Who posted it.</param>
/// <param name="CommunityId">Where it was posted.</param>
/// <param name="ActorId">The fediverse-wide identity.</param>
/// <param name="LanguageId">The language it was written in.</param>
/// <param name="IsNsfw">Whether it is flagged as not safe for work.</param>
/// <param name="IsLocked">Whether new comments are blocked.</param>
/// <param name="IsRemoved">Whether a moderator has removed it.</param>
/// <param name="IsDeleted">Whether the author has deleted it.</param>
/// <param name="IsFeaturedInCommunity">Whether it is pinned in its community.</param>
/// <param name="IsFeaturedLocally">Whether it is pinned on the instance front page.</param>
/// <param name="Published">When it was posted.</param>
/// <param name="Updated">When it was last edited, if ever.</param>
public sealed record Post(
    PostId Id,
    PostTitle Title,
    MarkdownText Body,
    WebLink? Url,
    WebLink? Thumbnail,
    MediaType? UrlMediaType,
    string? EmbedTitle,
    string? EmbedDescription,
    PersonId CreatorId,
    CommunityId CommunityId,
    ActorId ActorId,
    LanguageId LanguageId,
    bool IsNsfw,
    bool IsLocked,
    bool IsRemoved,
    bool IsDeleted,
    bool IsFeaturedInCommunity,
    bool IsFeaturedLocally,
    DateTimeOffset Published,
    DateTimeOffset? Updated)
{
    /// <summary>Whether the post is primarily a link to somewhere else.</summary>
    public bool IsLink => Url.HasValue;

    /// <summary>Whether the post is pinned anywhere the reader is currently looking.</summary>
    public bool IsFeatured => IsFeaturedInCommunity || IsFeaturedLocally;

    /// <summary>The image to show inline, preferring the server's thumbnail over the raw link.</summary>
    public WebLink? PreviewImage =>
        Thumbnail ?? (IsImage ? Url : null);

    /// <summary>
    /// Whether the post is a picture rather than an article — the thing worth opening full screen.
    /// The server's media type decides it when there is one, because image links routinely carry no
    /// file extension; the extension is only consulted when the server did not say.
    /// </summary>
    public bool IsImage =>
        UrlMediaType is { } mediaType ? mediaType.IsImage : Url is { LooksLikeImage: true };

    /// <summary>
    /// The best copy of the image to show full screen: the original link rather than the server's
    /// downscaled thumbnail, which exists for feed rows and looks poor blown up.
    /// </summary>
    public WebLink? FullImage => IsImage ? Url ?? Thumbnail : null;
}
