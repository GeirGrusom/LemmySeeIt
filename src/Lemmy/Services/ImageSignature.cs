namespace Lemmy.Services;

/// <summary>
/// Names a picture format from its first bytes. Used only once a decode has already failed, to say
/// which format it was: "that is an AVIF" is a different message from "that file is broken", and
/// the reader can do something about the first one.
/// </summary>
/// <remarks>
/// Deliberately not used to decide what to decode — the platform decoder is the authority on that,
/// and a sniffer that gets ahead of it would refuse things it can actually read. The server's
/// declared content type is no help here either: it is often absent on an inline picture, and a
/// picture that fails to decode is exactly the case where what the server said cannot be trusted.
/// </remarks>
internal static class ImageSignature
{
    /// <summary>The name of the format, or <see langword="null"/> when it is not one worth naming.</summary>
    internal static string? NameOf(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12)
        {
            return null;
        }

        // The ISO base media container: a length, then "ftyp", then the brand that says what it is.
        // AVIF and HEIF are both this, which is why neither can be told apart by extension alone.
        if (data[4..8].SequenceEqual("ftyp"u8))
        {
            return data[8..12] switch
            {
                var brand when brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8) => "AVIF",
                var brand when brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8)
                    || brand.SequenceEqual("hevc"u8) || brand.SequenceEqual("mif1"u8)
                    || brand.SequenceEqual("msf1"u8) => "HEIF",
                _ => null,
            };
        }

        // JPEG XL, in both the forms it comes in: the bare codestream and the boxed container.
        if (data[0] == 0xFF && data[1] == 0x0A)
        {
            return "JPEG XL";
        }

        ReadOnlySpan<byte> boxedJpegXl = [0x00, 0x00, 0x00, 0x0C, 0x4A, 0x58, 0x4C, 0x20, 0x0D, 0x0A, 0x87, 0x0A];
        if (data[..12].SequenceEqual(boxedJpegXl))
        {
            return "JPEG XL";
        }

        return IsSvg(data) ? "SVG" : null;
    }

    /// <summary>
    /// SVG is text, so it has no magic number — the best that can be done is to look for the tag
    /// near the front, past any declaration or comment.
    /// </summary>
    private static bool IsSvg(ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<byte> head = data[..Math.Min(data.Length, 512)];
        return head.IndexOf("<svg"u8) >= 0;
    }
}
