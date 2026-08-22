namespace Lemmy.Domain.Models;

/// <summary>How a comment thread is ordered within each level of nesting.</summary>
public enum CommentSortType
{
    /// <summary>Score weighted by age.</summary>
    Hot,

    /// <summary>Highest score first.</summary>
    Top,

    /// <summary>Newest first.</summary>
    New,

    /// <summary>Oldest first.</summary>
    Old,

    /// <summary>Most evenly split vote first.</summary>
    Controversial,
}
