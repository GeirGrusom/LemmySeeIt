using Lemmy.Domain.Models;

namespace Lemmy.Services;

/// <summary>
/// How much is waiting for the signed-in account, held where both the shell that shows the badge
/// and the page that changes it can reach it.
/// </summary>
/// <remarks>
/// The number is shown in the header and changed on the notification list, which are two pages
/// apart. Passing a callback down would work for one direction only: the shell also has to be able
/// to replace the whole tally when the server is asked again. Adjusting rather than recounting is
/// what keeps the badge honest when the list on screen is only the first page of a longer one.
/// </remarks>
public sealed class UnreadCounter
{
    /// <summary>Raised whenever the tally changes.</summary>
    public event EventHandler? Changed;

    /// <summary>What is currently waiting.</summary>
    public UnreadTally Tally { get; private set; }

    /// <summary>Takes the server's answer, which outranks anything counted locally.</summary>
    public void Set(UnreadTally tally)
    {
        if (Tally == tally)
        {
            return;
        }

        Tally = tally;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves the count by one notification being marked read or unread. A delta rather than a
    /// recount because the list on screen may be one page of several, so counting the rows there
    /// would silently shrink a badge that is telling the truth.
    /// </summary>
    public void Adjust(int replies, int mentions) =>
        Set(new UnreadTally(
            Tally.Replies + replies,
            Tally.Mentions + mentions,
            Tally.PrivateMessages));

    /// <summary>
    /// Everything has been marked read. Private messages go too: the endpoint that does this marks
    /// those as well, whether or not this app can show them.
    /// </summary>
    public void Clear() => Set(UnreadTally.None);
}
