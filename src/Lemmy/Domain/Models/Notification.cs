using Lemmy.Domain;

namespace Lemmy.Domain.Models;

/// <summary>
/// Something somebody wrote that was addressed to the signed-in account: a reply to one of their
/// posts or comments, or a comment that named them. Lemmy sends the comment with the post and
/// community already joined on, which is what makes a notification openable from the list.
/// </summary>
/// <param name="Kind">Which list it came from, which decides how it is marked read.</param>
/// <param name="Id">Its row in that list — not the comment's own identifier.</param>
/// <param name="Comment">What was written.</param>
/// <param name="Creator">Who wrote it.</param>
/// <param name="Post">The post it was written on.</param>
/// <param name="Community">Where that post lives.</param>
/// <param name="Tally">The comment's vote counts.</param>
/// <param name="Received">When the notification was raised, which is not always when the comment was published.</param>
/// <param name="IsRead">Whether the account has already seen it.</param>
/// <param name="MyVote">How the account voted on the comment.</param>
public sealed record Notification(
    NotificationKind Kind,
    NotificationId Id,
    Comment Comment,
    Person Creator,
    Post Post,
    Community Community,
    CommentTally Tally,
    DateTimeOffset Received,
    bool IsRead,
    Vote MyVote = Vote.None)
{
    /// <summary>The post to open when the notification is tapped.</summary>
    public PostId PostId => Post.Id;
}
