using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.ViewModels;

namespace Lemmy.Services;

/// <summary>
/// How a page moves to another page. Pages depend on this rather than on the shell so that a feed
/// can be tested end to end without a window ever existing.
/// </summary>
public interface INavigator
{
    /// <summary>Pushes <paramref name="page"/> on top of the current one.</summary>
    void Push(PageViewModel page);

    /// <summary>Returns to the previous page; does nothing at the root.</summary>
    void Pop();

    /// <summary>Whether there is anywhere to go back to.</summary>
    bool CanPop { get; }

    /// <summary>
    /// Shows a post's image full screen, over everything else. Not a push: the viewer is an overlay
    /// rather than a page, so that an art or comic community can be browsed picture by picture
    /// without the surrounding chrome and without leaving the feed at all.
    /// </summary>
    /// <param name="summary">The post whose picture to show first.</param>
    /// <param name="gallery">The page it came from, so the reader can move on to its other pictures.</param>
    void ShowImage(PostSummary summary, IImageGallery gallery);

    /// <summary>
    /// Shows a picture that is not a post — one written into a post body or a comment. There is no
    /// page of pictures around it, so the viewer opens on this one alone.
    /// </summary>
    /// <param name="picture">The picture to show.</param>
    /// <param name="caption">What to call it, usually the author's alt text.</param>
    void ShowPicture(WebLink picture, string caption);

    /// <summary>
    /// Opens an account's page. Whether it offers to sign out is decided there, by whether this is
    /// the reader's own account — not by whoever asked for it.
    /// </summary>
    void ShowProfile(PersonId person);
}
