using System.Collections.Immutable;
using Lemmy.Domain.Models;

namespace Lemmy.Tests.Domain;

/// <summary>
/// <see cref="PackageAttribution"/> is built from generated code fed by whatever a package's
/// .nuspec happens to contain, which is not a contract anyone enforces. It has to tolerate every
/// field being absent rather than throw during startup.
/// </summary>
[TestFixture]
internal sealed class PackageAttributionTests
{
    [Test]
    public void AFullyDescribedPackageKeepsEveryField()
    {
        PackageAttribution package = PackageAttribution.Create(
            "Avalonia", "12.1.1", "MIT", "Copyright the AvaloniaUI Project", "https://avaloniaui.net/");

        Assert.Multiple(() =>
        {
            Assert.That(package.IsValid, Is.True);
            Assert.That(package.Id, Is.EqualTo("Avalonia"));
            Assert.That(package.Version, Is.EqualTo("12.1.1"));
            Assert.That(package.Licence, Is.EqualTo("MIT"));
            Assert.That(package.Copyright, Is.EqualTo("Copyright the AvaloniaUI Project"));
            Assert.That(package.ProjectLink.Value, Is.EqualTo("https://avaloniaui.net/"));
            Assert.That(package.HasUndeclaredLicence, Is.False);
        });
    }

    [Test]
    public void APackageThatDeclaresNothingButANameIsStillListed()
    {
        PackageAttribution package = PackageAttribution.Create("Mystery", null, null, null, null);

        Assert.Multiple(() =>
        {
            Assert.That(package.IsValid, Is.True);
            Assert.That(package.HasUndeclaredLicence, Is.True);
            Assert.That(package.ProjectLink.IsValid, Is.False);
            Assert.That(package.Copyright, Is.Empty);
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void APackageWithNoNameIsNotAnEntryAtAll(string? id)
    {
        Assert.That(PackageAttribution.Create(id, "1.0", "MIT", null, null).IsValid, Is.False);
    }

    [Test]
    public void AProjectUrlThatIsNotALinkIsDroppedRatherThanThrowing()
    {
        PackageAttribution package = PackageAttribution.Create("X", "1.0", "MIT", null, "not a url");

        Assert.That(package.ProjectLink.IsValid, Is.False);
    }

    [Test]
    public void ACompoundLicenceCountsAsEachOfItsParts()
    {
        ReadOnlySpan<PackageAttribution> packages =
        [
            PackageAttribution.Create("A", "1", "MIT AND Apache-2.0", null, null),
            PackageAttribution.Create("B", "1", "MIT", null, null),
            PackageAttribution.Create("C", "1", "BSD-2-Clause", null, null),
        ];

        Assert.That(
            PackageAttribution.DistinctLicences(packages),
            Is.EqualTo(new[] { "Apache-2.0", "BSD-2-Clause", "MIT" }));
    }

    [Test]
    public void ALicenceGivenAsAUrlIsNotTreatedAsAnIdentifier()
    {
        ReadOnlySpan<PackageAttribution> packages =
        [
            PackageAttribution.Create("A", "1", "https://example.com/licence", null, null),
            PackageAttribution.Create("B", "1", "", null, null),
            PackageAttribution.Create("C", "1", "MIT", null, null),
        ];

        Assert.That(PackageAttribution.DistinctLicences(packages), Is.EqualTo(new[] { "MIT" }));
    }
}
