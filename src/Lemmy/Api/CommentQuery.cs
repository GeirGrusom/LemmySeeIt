using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Api;

/// <summary>What to ask a post's comment thread for.</summary>
/// <param name="Post">The post whose comments to fetch.</param>
/// <param name="Sort">How to order siblings at each level of nesting.</param>
/// <param name="MaxDepth">How many levels of replies to fetch in one round trip.</param>
/// <param name="PageSize">How many comments to ask for.</param>
/// <param name="Parent">A comment to fetch the sub-thread of, for "load more replies".</param>
public readonly record struct CommentQuery(
    PostId Post,
    CommentSortType Sort = CommentSortType.Hot,
    CommentDepth MaxDepth = default,
    PageSize PageSize = default,
    CommentId? Parent = null);
