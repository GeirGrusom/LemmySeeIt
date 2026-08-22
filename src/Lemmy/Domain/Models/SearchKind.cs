namespace Lemmy.Domain.Models;

/// <summary>What a search should look through.</summary>
public enum SearchKind
{
    /// <summary>Every kind at once.</summary>
    All,

    /// <summary>Post titles and bodies.</summary>
    Posts,

    /// <summary>Comment bodies.</summary>
    Comments,

    /// <summary>Community names, titles and sidebars.</summary>
    Communities,

    /// <summary>Account names.</summary>
    Users,
}
