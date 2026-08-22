using System.Collections.Immutable;
using Lemmy.Domain.Models;

namespace Lemmy.ViewModels;

/// <summary>
/// A list the image viewer can move through — the pictures on whatever page opened it, in the order
/// they appear there.
/// </summary>
/// <remarks>
/// Read afresh on each move rather than snapshotted when the viewer opens, so that pictures the
/// feed has loaded in the meantime are there to flick to. That matters on a page that keeps growing
/// as it is scrolled: freezing the list at open would strand the reader at whatever happened to be
/// loaded at the moment they tapped.
/// </remarks>
public interface IImageGallery
{
    /// <summary>The posts on this page that are pictures, in page order.</summary>
    ImmutableArray<PostSummary> Images { get; }

    /// <summary>Whether the page has more posts it could fetch.</summary>
    bool CanLoadMore { get; }

    /// <summary>
    /// Fetches the page's next batch. Called by the viewer when the reader reaches the last
    /// picture: the page is underneath a full-screen picture and cannot be scrolled, so without
    /// this the gallery ends wherever the reader happened to have scrolled to before opening it.
    /// </summary>
    Task LoadMoreAsync();
}
