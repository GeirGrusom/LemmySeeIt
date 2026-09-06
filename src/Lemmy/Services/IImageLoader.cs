using Avalonia.Media.Imaging;
using Lemmy.Domain;

namespace Lemmy.Services;

/// <summary>Fetches and decodes remote images for the UI.</summary>
public interface IImageLoader
{
    /// <summary>
    /// Loads <paramref name="link"/>, decoded to at most <paramref name="decodeWidth"/> pixels wide.
    /// Returns <see langword="null"/> rather than throwing when the image cannot be fetched or
    /// decoded: a broken thumbnail should leave a gap in the feed, not an error dialog.
    /// </summary>
    Task<Bitmap?> LoadAsync(WebLink link, int decodeWidth, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads <paramref name="link"/> for the full-size viewer, animated when it has more than one
    /// frame. Not cached: the shared cache is bounded by entry count, which is fine for thumbnails
    /// and would be a memory problem for full-resolution pictures and their frames. The caller owns
    /// the result and must dispose it.
    /// </summary>
    /// <returns>
    /// The picture, or why there isn't one. Unlike <see cref="LoadAsync"/> this reports the reason,
    /// because both of its callers have somewhere to put it.
    /// </returns>
    Task<PictureLoad> LoadPictureAsync(WebLink link, int decodeWidth, CancellationToken cancellationToken = default);
}
