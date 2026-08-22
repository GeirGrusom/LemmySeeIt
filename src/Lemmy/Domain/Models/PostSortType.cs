namespace Lemmy.Domain.Models;

/// <summary>How a post listing is ordered.</summary>
public enum PostSortType
{
    /// <summary>Recent activity weighted by score — Lemmy's default front page.</summary>
    Active,

    /// <summary>Score weighted by age, favouring fast risers.</summary>
    Hot,

    /// <summary>Score over a rolling window, favouring today's best.</summary>
    Scaled,

    /// <summary>Newest first.</summary>
    New,

    /// <summary>Oldest first.</summary>
    Old,

    /// <summary>Most-commented first.</summary>
    MostComments,

    /// <summary>Posts with new comments first.</summary>
    NewComments,

    /// <summary>Highest score in the last six hours.</summary>
    TopSixHour,

    /// <summary>Highest score in the last day.</summary>
    TopDay,

    /// <summary>Highest score in the last week.</summary>
    TopWeek,

    /// <summary>Highest score in the last month.</summary>
    TopMonth,

    /// <summary>Highest score in the last year.</summary>
    TopYear,

    /// <summary>Highest score of all time.</summary>
    TopAll,

    /// <summary>Posts with the most evenly split vote, i.e. the arguments.</summary>
    Controversial,
}
