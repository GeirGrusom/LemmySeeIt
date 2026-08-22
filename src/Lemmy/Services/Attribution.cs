using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Lemmy.Domain.Models;

namespace Lemmy.Services;

/// <summary>
/// What the app has to say about the code it is built from: its own licence, the licence of the
/// typeface it embeds, and every third-party package it ships.
/// </summary>
/// <remarks>
/// The package list is generated at build time from the packages whose assets are actually copied
/// beside the binary — see <c>build/Attribution.targets</c> — so it cannot drift from what is
/// shipped. Each head ships a different set (the Android head carries AndroidX, the desktop heads
/// carry X11 and D-Bus), so a head replaces the list at startup through <see cref="Use"/>; the
/// shared project's own set is the default, which is what the tests see.
/// </remarks>
public static class Attribution
{
    private const string ResourcePrefix = "Lemmy.Licences.";

    private static ImmutableArray<PackageAttribution> packages = Generated.GeneratedAttribution.Packages;

    /// <summary>Every third-party package shipped with this head, ordered by name.</summary>
    public static ImmutableArray<PackageAttribution> Packages => packages;

    /// <summary>
    /// Replaces the package list with the one generated for the running head. Called once, before
    /// the shell is built.
    /// </summary>
    public static void Use(ImmutableArray<PackageAttribution> value)
    {
        if (!value.IsDefaultOrEmpty)
        {
            packages = value;
        }
    }

    /// <summary>This app's own licence, in full.</summary>
    public static string ApplicationLicence { get; } = Read("LemmySeeIt.txt");

    /// <summary>
    /// The notice for the Inter typeface, which the app embeds through <c>Avalonia.Fonts.Inter</c>.
    /// The font is under the SIL Open Font License rather than the MIT of the package that carries
    /// it, and the OFL asks that this notice travel with the font — which is why it is here and not
    /// only in the repository.
    /// </summary>
    public static string FontNotice { get; } = Read("Inter-OFL-1.1.txt");

    /// <summary>
    /// The full text of a licence the shipped packages name, for example <c>MIT</c>. Returns
    /// <see langword="false"/> for one whose text is not carried — a package that declares only a
    /// URL, or names a licence nothing else here uses.
    /// </summary>
    public static bool TryGetLicenceText(string identifier, out string text)
    {
        text = string.IsNullOrWhiteSpace(identifier) ? string.Empty : Read(identifier.Trim() + ".txt");
        return text.Length > 0;
    }

    private static string Read(string fileName)
    {
        Assembly assembly = typeof(Attribution).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(ResourcePrefix + fileName);
        if (stream is null)
        {
            return string.Empty;
        }

        using StreamReader reader = new(stream);
        return Reflow(reader.ReadToEnd().TrimEnd());
    }

    /// <summary>
    /// Unwraps prose so the view can wrap it to the screen instead. Licence files are hard-wrapped
    /// to a fixed column, which on a phone wraps a second time and leaves a ragged half-line after
    /// every full one.
    /// </summary>
    /// <remarks>
    /// Only paragraphs whose every line starts hard against the margin are joined. An indented line
    /// is carrying layout — Apache-2.0's hanging definitions, an ASCII heading — and reflowing that
    /// would destroy it, so such a paragraph is left exactly as written. Blank lines always survive,
    /// so the paragraph structure is unchanged either way.
    /// </remarks>
    internal static string Reflow(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        StringBuilder result = new(text.Length);
        foreach (Range block in text.AsSpan().Split("\n\n"))
        {
            ReadOnlySpan<char> paragraph = text.AsSpan()[block];
            if (result.Length > 0)
            {
                result.Append("\n\n");
            }

            if (IsIndented(paragraph))
            {
                result.Append(paragraph);
                continue;
            }

            ReadOnlySpan<char> pending = default;
            bool first = true;
            foreach (Range line in paragraph.Split('\n'))
            {
                ReadOnlySpan<char> current = paragraph[line].Trim();
                if (current.IsEmpty)
                {
                    continue;
                }

                if (!first)
                {
                    result.Append(Continues(pending, current) ? ' ' : '\n');
                }

                result.Append(current);
                pending = current;
                first = false;
            }
        }

        return result.ToString();
    }

    /// <summary>Whether any line in the paragraph is carrying layout in its leading whitespace.</summary>
    private static bool IsIndented(ReadOnlySpan<char> paragraph)
    {
        foreach (Range line in paragraph.Split('\n'))
        {
            ReadOnlySpan<char> candidate = paragraph[line];
            if (candidate.Length > 0 && char.IsWhiteSpace(candidate[0]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="line"/> is a wrapped line that runs on into <paramref name="next"/>,
    /// rather than a line that was meant to stand alone.
    /// </summary>
    /// <remarks>
    /// Hard-wrapped prose fills its column, so a line well short of the wrap width ended
    /// deliberately — a heading like OFL's "PREAMBLE", or the last line of a paragraph. A line of
    /// rules with no letters in it is a divider, and joining anything to it runs the divider into
    /// the text it was separating.
    /// </remarks>
    private static bool Continues(ReadOnlySpan<char> line, ReadOnlySpan<char> next) =>
        line.Length >= 55 && HasLetterOrDigit(line) && HasLetterOrDigit(next);

    private static bool HasLetterOrDigit(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                return true;
            }
        }

        return false;
    }
}
