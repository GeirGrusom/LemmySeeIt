namespace Lemmy.Services;

/// <summary>Why a picture is not on screen.</summary>
public enum ImageFailure
{
    /// <summary>It is: nothing went wrong.</summary>
    None,

    /// <summary>The bytes never arrived — no route, a refusal, a timeout, or too many of them.</summary>
    Unreachable,

    /// <summary>The bytes arrived, and they are a picture in a format nothing here can decode.</summary>
    UnsupportedFormat,

    /// <summary>The bytes arrived and could not be made sense of at all.</summary>
    Undecodable,
}

/// <summary>
/// The outcome of asking for a picture: the picture, or why there isn't one.
/// </summary>
/// <remarks>
/// The reason travels with the result rather than being worked out again by the caller, because by
/// the time a caller notices it got nothing, the bytes that would explain it are gone. Feed
/// thumbnails still ask for a plain bitmap and still just leave a gap; this is for the two places
/// that have room to say something — the full-screen viewer and a picture inside a post or comment.
/// </remarks>
/// <param name="Picture">What was loaded, or <see langword="null"/>.</param>
/// <param name="Failure">Why there is nothing, or <see cref="ImageFailure.None"/>.</param>
/// <param name="FormatName">The format it turned out to be, when that is worth naming.</param>
public readonly record struct PictureLoad(AnimatedImage? Picture, ImageFailure Failure, string? FormatName = null)
{
    /// <summary>A picture that loaded.</summary>
    public static PictureLoad Loaded(AnimatedImage picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        return new PictureLoad(picture, ImageFailure.None);
    }

    /// <summary>The bytes never arrived.</summary>
    public static PictureLoad Unreachable { get; } = new(null, ImageFailure.Unreachable);

    /// <summary>The bytes arrived and could not be decoded; <paramref name="formatName"/> when it is known.</summary>
    public static PictureLoad CannotDecode(string? formatName) =>
        new(null, formatName is null ? ImageFailure.Undecodable : ImageFailure.UnsupportedFormat, formatName);

    /// <summary>Whether there is a picture.</summary>
    public bool Succeeded => Picture is not null;

    /// <summary>
    /// What to put on screen instead of the picture. Lives here rather than in each view so that
    /// the viewer and an inline picture cannot drift into saying different things about the same
    /// failure.
    /// </summary>
    public string Message => Failure switch
    {
        ImageFailure.None => string.Empty,
        ImageFailure.Unreachable => "That picture could not be downloaded.",
        ImageFailure.UnsupportedFormat => $"That picture is {Article(FormatName!)} {FormatName}, which this app cannot show.",
        _ => "That picture is damaged, or in a format this app cannot show.",
    };

    private static string Article(string formatName) =>
        formatName.Length > 0 && "AEIOU".Contains(formatName[0], StringComparison.OrdinalIgnoreCase) ? "an" : "a";
}
