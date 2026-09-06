using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Api;

/// <summary>What to ask the notification lists for.</summary>
/// <param name="Sort">How to order them; <see cref="CommentSortType.New"/> is what a notification list wants.</param>
/// <param name="Page">Which one-based page to fetch — these list by number, not by cursor.</param>
/// <param name="PageSize">How many to ask each list for.</param>
/// <param name="UnreadOnly">Whether to leave out what has already been seen.</param>
public readonly record struct NotificationQuery(
    CommentSortType Sort = CommentSortType.New,
    int Page = 1,
    PageSize PageSize = default,
    bool UnreadOnly = true)
{
    /// <summary>The page number, floored at one so a <see langword="default"/> query is still valid.</summary>
    public int PageNumber => Math.Max(1, Page);
}
