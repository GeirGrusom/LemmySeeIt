namespace Lemmy.Domain;

/// <summary>How the signed-in account voted on something.</summary>
public enum Vote
{
    /// <summary>Voted down.</summary>
    Down = -1,

    /// <summary>Not voted, or nobody is signed in.</summary>
    None = 0,

    /// <summary>Voted up.</summary>
    Up = 1,
}

/// <summary>Reads the vote Lemmy sends as a number.</summary>
public static class VoteExtensions
{
    /// <summary>
    /// Maps the wire value. Absent means "nobody is signed in", which is the same as not having
    /// voted as far as anything on screen is concerned.
    /// </summary>
    public static Vote ToVote(this int? value) => value switch
    {
        > 0 => Vote.Up,
        < 0 => Vote.Down,
        _ => Vote.None,
    };

    /// <summary>
    /// What pressing <paramref name="pressed"/> does to <paramref name="current"/>. Pressing the
    /// arrow you already chose takes the vote back rather than casting it again, which is what every
    /// site with these arrows does and what Lemmy's own score of <c>0</c> means.
    /// </summary>
    public static Vote Toggle(this Vote current, Vote pressed) => current == pressed ? Vote.None : pressed;

    /// <summary>The number Lemmy wants for a vote: <c>1</c>, <c>0</c> or <c>-1</c>.</summary>
    public static int ToScore(this Vote vote) => (int)vote;
}
