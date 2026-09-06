using System.Globalization;

namespace Lemmy.Domain.Models;

/// <summary>
/// How much is waiting for the signed-in account. Clamped at zero on the way in: the three numbers
/// are counted separately by the server and a negative one would only ever be a bug on its side.
/// </summary>
public readonly record struct UnreadTally
{
    /// <summary>Above this the badge stops counting and starts saying "lots".</summary>
    public const int MostWorthCounting = 99;

    /// <summary>Takes the three counts the server reports.</summary>
    public UnreadTally(int replies, int mentions, int privateMessages)
    {
        Replies = Math.Max(0, replies);
        Mentions = Math.Max(0, mentions);
        PrivateMessages = Math.Max(0, privateMessages);
    }

    /// <summary>Replies to the account's posts and comments.</summary>
    public int Replies { get; }

    /// <summary>Comments that wrote the account's name.</summary>
    public int Mentions { get; }

    /// <summary>
    /// Direct messages. Counted because the server sends the number, but deliberately left out of
    /// <see cref="Total"/>: this app has nowhere to read a message, so counting them would put a
    /// badge on screen that nothing the reader can do would ever clear.
    /// </summary>
    public int PrivateMessages { get; }

    /// <summary>Nothing waiting.</summary>
    public static UnreadTally None => default;

    /// <summary>What the badge counts: the things this app can actually show.</summary>
    public int Total => Replies + Mentions;

    /// <summary>Whether there is anything to show a badge for.</summary>
    public bool Any => Total > 0;

    /// <summary>The badge text, which stops being a number once it stops being worth reading.</summary>
    public string Label => Total > MostWorthCounting
        ? MostWorthCounting.ToString(CultureInfo.InvariantCulture) + "+"
        : Total.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public override string ToString() => Label;
}
