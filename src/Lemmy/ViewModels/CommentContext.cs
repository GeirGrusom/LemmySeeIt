using System.Windows.Input;
using Lemmy.Api;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>
/// Everything a comment needs that is the same for every comment in the thread. Handed down the
/// tree as one value rather than as eight arguments: a thread is hundreds of these, and they all
/// vote, copy, render pictures and open links through the same objects.
/// </summary>
/// <param name="Api">The client the thread votes, replies and fetches missing replies through.</param>
/// <param name="Account">Who is signed in, which decides whose comments can be edited.</param>
/// <param name="Media">How pictures written into a comment are fetched and opened.</param>
/// <param name="Copier">Where the copy button puts what the author wrote.</param>
/// <param name="Navigator">Where an author's name leads.</param>
/// <param name="Now">The time the thread was drawn at, so every age label agrees.</param>
/// <param name="Sort">
/// The sort the thread was fetched with. Carried so that replies fetched later arrive in the same
/// order as the ones already on screen, rather than in the server's default.
/// </param>
/// <param name="LinkCommand">Opens a link pressed inside a comment.</param>
public sealed record CommentContext(
    ILemmyApi Api,
    CurrentAccount Account,
    MarkdownMedia Media,
    ITextCopier Copier,
    INavigator Navigator,
    DateTimeOffset Now,
    CommentSortType Sort = CommentSortType.Hot,
    ICommand? LinkCommand = null);
