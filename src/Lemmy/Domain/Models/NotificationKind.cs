namespace Lemmy.Domain.Models;

/// <summary>
/// Which of Lemmy's two notification lists something came from. They are separate endpoints with
/// separately numbered rows and separate ways to mark one read, so the kind has to travel with the
/// identifier everywhere it goes.
/// </summary>
public enum NotificationKind
{
    /// <summary>Somebody replied to a post or a comment of yours.</summary>
    Reply,

    /// <summary>Somebody wrote your name into a comment.</summary>
    Mention,
}
