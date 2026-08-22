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
}
