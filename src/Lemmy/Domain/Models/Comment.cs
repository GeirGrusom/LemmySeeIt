namespace Lemmy.Domain.Models;

/// <summary>A comment, stripped of the vote tallies and viewer-specific flags that travel with it.</summary>
/// <param name="Id">The instance-local identifier.</param>
/// <param name="PostId">The post it belongs to.</param>
/// <param name="CreatorId">Who wrote it.</param>
/// <param name="Content">The Markdown body.</param>
/// <param name="Path">The ancestor chain that places it in the thread.</param>
/// <param name="ActorId">The fediverse-wide identity.</param>
/// <param name="LanguageId">The language it was written in.</param>
/// <param name="IsRemoved">Whether a moderator has removed it.</param>
/// <param name="IsDeleted">Whether the author has deleted it.</param>
/// <param name="IsDistinguished">Whether a moderator posted it in an official capacity.</param>
/// <param name="Published">When it was posted.</param>
/// <param name="Updated">When it was last edited, if ever.</param>
public sealed record Comment(
    CommentId Id,
    PostId PostId,
    PersonId CreatorId,
    MarkdownText Content,
    CommentPath Path,
    ActorId ActorId,
    LanguageId LanguageId,
    bool IsRemoved,
    bool IsDeleted,
    bool IsDistinguished,
    DateTimeOffset Published,
    DateTimeOffset? Updated)
{
    /// <summary>
    /// What to render. Removed and deleted comments still arrive with their original text on some
    /// instances, so the placeholder is substituted here rather than trusted to the server.
    /// </summary>
    public MarkdownText VisibleContent => (IsRemoved, IsDeleted) switch
    {
        (true, _) => new MarkdownText("*Removed by moderator*"),
        (_, true) => new MarkdownText("*Deleted by author*"),
        _ => Content,
    };
}
