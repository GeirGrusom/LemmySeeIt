using System.Collections.Immutable;

namespace Lemmy.Domain.Models;

/// <summary>
/// One third-party package shipped with the app, as read from its <c>.nuspec</c> at build time.
/// Every field is optional on the wire — a package can decline to declare a licence, a copyright or
/// a project page — so this type is built through <see cref="Create"/>, which never throws. An
/// attribution page that crashed on a malformed package would be worse than one with a gap in it.
/// </summary>
public readonly record struct PackageAttribution
{
    private readonly string? id;

    private PackageAttribution(
        string id,
        string version,
        string licence,
        string copyright,
        WebLink projectLink)
    {
        this.id = id;
        Version = version;
        Licence = licence;
        Copyright = copyright;
        ProjectLink = projectLink;
    }

    /// <summary>The package id, for example <c>Avalonia</c>.</summary>
    public string Id => id ?? string.Empty;

    /// <summary>The exact version shipped.</summary>
    public string Version { get; }

    /// <summary>
    /// The SPDX expression the package declares — <c>MIT</c>, <c>MIT AND Apache-2.0</c> — or empty
    /// when it declares none.
    /// </summary>
    public string Licence { get; }

    /// <summary>The package's own copyright line, or empty when it carries none.</summary>
    public string Copyright { get; }

    /// <summary>The project page, or an invalid link when the package names none.</summary>
    public WebLink ProjectLink { get; }

    /// <summary><see langword="false"/> for <see langword="default"/>.</summary>
    public bool IsValid => !string.IsNullOrEmpty(id);

    /// <summary><see langword="true"/> when the package declares no licence at all.</summary>
    public bool HasUndeclaredLicence => Licence.Length == 0;

    /// <summary>
    /// Builds an entry, tolerating anything. Called from generated code, so it takes plain strings
    /// and validates them here rather than pushing that into the generator.
    /// </summary>
    public static PackageAttribution Create(
        string? id,
        string? version,
        string? licence,
        string? copyright,
        string? projectUrl)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return default;
        }

        _ = WebLink.TryParse(projectUrl.AsSpan(), out WebLink link);

        return new PackageAttribution(
            id.Trim(),
            version?.Trim() ?? string.Empty,
            licence?.Trim() ?? string.Empty,
            copyright?.Trim() ?? string.Empty,
            link);
    }

    /// <summary>
    /// The distinct licence identifiers across a set of packages, so the page can show the full text
    /// of each one in play. A compound <c>MIT AND Apache-2.0</c> counts as both.
    /// </summary>
    public static ImmutableArray<string> DistinctLicences(ReadOnlySpan<PackageAttribution> packages)
    {
        SortedSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase);
        foreach (PackageAttribution package in packages)
        {
            foreach (Range part in package.Licence.AsSpan().Split(" AND "))
            {
                ReadOnlySpan<char> identifier = package.Licence.AsSpan()[part].Trim();

                // A licence given as a URL rather than an SPDX id has no text to show.
                if (identifier.Length > 0 && !identifier.Contains("://", StringComparison.Ordinal))
                {
                    identifiers.Add(identifier.ToString());
                }
            }
        }

        return [.. identifiers];
    }

    /// <inheritdoc />
    public override string ToString() => Version.Length == 0 ? Id : $"{Id} {Version}";
}
