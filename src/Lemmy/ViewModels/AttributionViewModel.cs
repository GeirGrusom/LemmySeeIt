using System.Collections.Immutable;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>
/// The licences page: this app's own terms, the notice for the typeface it embeds, and every
/// third-party package it ships. Everything shown is generated at build time from the packages
/// actually copied beside the binary, so the page cannot fall behind the dependencies.
/// </summary>
public sealed partial class AttributionViewModel : PageViewModel
{
    /// <summary>Creates the page.</summary>
    public AttributionViewModel(AppServices services, INavigator navigator)
        : base(services, navigator)
    {
        Packages = Attribution.Packages;

        ImmutableArray<string> identifiers = PackageAttribution.DistinctLicences(Packages.AsSpan());
        ImmutableArray<LicenceText>.Builder texts = ImmutableArray.CreateBuilder<LicenceText>(identifiers.Length);
        foreach (string identifier in identifiers)
        {
            if (Attribution.TryGetLicenceText(identifier, out string body))
            {
                texts.Add(new LicenceText(identifier, body));
            }
        }

        LicenceTexts = texts.ToImmutable();
    }

    /// <inheritdoc />
    public override string Title => "Licences";

    /// <summary>This app's own licence, in full.</summary>
    public string ApplicationLicence => Attribution.ApplicationLicence;

    /// <summary>The notice the SIL Open Font License asks travel with the Inter typeface.</summary>
    public string FontNotice => Attribution.FontNotice;

    /// <summary>Every third-party package shipped with this build, ordered by name.</summary>
    public ImmutableArray<PackageAttribution> Packages { get; }

    /// <summary>The full text of each licence those packages are under.</summary>
    public ImmutableArray<LicenceText> LicenceTexts { get; }

    /// <summary>How many packages there are, for the section heading.</summary>
    public string PackageCount =>
        Packages.Length == 1 ? "1 package" : $"{Packages.Length} packages";

    /// <summary>Nothing to fetch: the page is built from what shipped.</summary>
    public override Task LoadAsync() => Task.CompletedTask;

    /// <summary>Opens a package's project page in the browser.</summary>
    [RelayCommand]
    private async Task OpenProjectAsync(PackageAttribution package)
    {
        if (package.ProjectLink.IsValid)
        {
            await Services.LinkOpener.OpenAsync(package.ProjectLink).ConfigureAwait(true);
        }
    }
}

/// <summary>The full text of one licence, keyed by its SPDX identifier.</summary>
/// <param name="Identifier">The SPDX identifier, for example <c>MIT</c>.</param>
/// <param name="Body">The licence text, without any per-package copyright line.</param>
public readonly record struct LicenceText(string Identifier, string Body);
