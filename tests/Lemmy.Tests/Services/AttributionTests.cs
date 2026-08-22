using System.Collections.Immutable;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.Tests.Services;

/// <summary>
/// The attribution data is generated at build time from the packages the build actually ships, so
/// these assert the shape of what the generator produces and that the licence texts it refers to
/// are really embedded — a page that silently showed nothing would look the same as a correct one.
/// </summary>
[TestFixture]
internal sealed class AttributionTests
{
    [Test]
    public void TheGeneratedListNamesThePackagesTheAppIsBuiltFrom()
    {
        ImmutableArray<PackageAttribution> packages = Attribution.Packages;

        Assert.Multiple(() =>
        {
            Assert.That(packages, Is.Not.Empty);
            Assert.That(packages.Select(p => p.Id), Does.Contain("Avalonia").And.Contain("Markdig"));
            Assert.That(packages.All(p => p.IsValid), Is.True);
        });
    }

    [Test]
    public void EveryEntryCarriesAVersion()
    {
        Assert.That(Attribution.Packages.Where(p => p.Version.Length == 0), Is.Empty);
    }

    [Test]
    public void ThePackagesAreOrderedByNameAndNotRepeated()
    {
        ImmutableArray<string> names = [.. Attribution.Packages.Select(p => p.Id)];

        Assert.Multiple(() =>
        {
            Assert.That(names, Is.Ordered.Using<string>(StringComparer.OrdinalIgnoreCase));
            Assert.That(names, Is.Unique);
        });
    }

    [Test]
    public void TheApplicationLicenceIsTheRepositoryLicence()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Attribution.ApplicationLicence, Does.StartWith("MIT License"));
            Assert.That(Attribution.ApplicationLicence, Does.Contain("Henning Moe"));
            Assert.That(Attribution.ApplicationLicence, Does.Contain("WITHOUT WARRANTY OF ANY KIND"));
        });
    }

    [Test]
    public void TheFontNoticeCarriesTheOpenFontLicenceAndItsCopyright()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Attribution.FontNotice, Does.Contain("The Inter Project Authors"));
            Assert.That(Attribution.FontNotice, Does.Contain("SIL OPEN FONT LICENSE Version 1.1"));
        });
    }

    [Test]
    public void EveryLicenceThePackagesNameHasItsTextEmbedded()
    {
        ImmutableArray<string> identifiers =
            PackageAttribution.DistinctLicences(Attribution.Packages.AsSpan());

        Assert.That(identifiers, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (string identifier in identifiers)
            {
                Assert.That(
                    Attribution.TryGetLicenceText(identifier, out string text),
                    Is.True,
                    $"no embedded text for {identifier}");
                Assert.That(text, Is.Not.Empty);
            }
        });
    }

    [Test]
    public void ALicenceWithNoEmbeddedTextIsReportedRatherThanFakedUp()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Attribution.TryGetLicenceText("NoSuchLicence-9.9", out string missing), Is.False);
            Assert.That(missing, Is.Empty);
            Assert.That(Attribution.TryGetLicenceText("", out _), Is.False);
        });
    }

    [Test]
    public void HardWrappedProseIsJoinedSoTheViewCanWrapItInstead()
    {
        // Lines as a licence file wraps them: filled to the column, so they run on.
        string reflowed = Attribution.Reflow(
            "Permission is hereby granted, free of charge, to any person obtaining\na copy of this software and associated documentation files.");

        Assert.That(
            reflowed,
            Is.EqualTo("Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files."));
    }

    [Test]
    public void BlankLinesSurviveSoParagraphsStayApart()
    {
        string reflowed = Attribution.Reflow(
            "The above copyright notice and this permission notice shall be included\nin all copies.\n\n"
            + "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS\nOR IMPLIED.");

        Assert.That(
            reflowed,
            Is.EqualTo("The above copyright notice and this permission notice shall be included in all copies.\n\n"
                + "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED."));
    }

    [Test]
    public void AnIndentedParagraphIsLeftExactlyAsWritten()
    {
        // Apache-2.0's definitions hang on their indentation; joining them would destroy the layout.
        const string Original = "   \"License\" shall mean\n   the terms and conditions.";

        Assert.That(Attribution.Reflow(Original), Is.EqualTo(Original));
    }

    [Test]
    public void TheApacheLicenceKeepsItsIndentedStructure()
    {
        Assert.That(Attribution.TryGetLicenceText("Apache-2.0", out string text), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Apache License"));
            Assert.That(text, Does.Contain("\n   "), "indented blocks should survive reflow");
        });
    }

    [Test]
    public void ReflowLeavesEmptyTextAlone()
    {
        Assert.That(Attribution.Reflow(string.Empty), Is.Empty);
    }

    [Test]
    public void AHeadingKeepsItsOwnLineEvenWithNoBlankLineAfterIt()
    {
        // The OFL writes "PREAMBLE" directly above its paragraph; joining the two read as prose.
        string reflowed = Attribution.Reflow(
            "PREAMBLE\nThe goals of the Open Font License (OFL) are to stimulate worldwide\ndevelopment of collaborative font projects.");

        Assert.That(
            reflowed,
            Is.EqualTo("PREAMBLE\nThe goals of the Open Font License (OFL) are to stimulate worldwide development of collaborative font projects."));
    }

    [Test]
    public void ARuleOfDashesIsNotRunIntoTheTextItSeparates()
    {
        string reflowed = Attribution.Reflow(
            "-----------------------------------------------------------\nSIL OPEN FONT LICENSE Version 1.1 - 26 February 2007\n-----------------------------------------------------------");

        Assert.That(reflowed.Split('\n'), Has.Length.EqualTo(3));
    }

    [Test]
    public void TheFontNoticeKeepsItsHeadingsOnTheirOwnLines()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Attribution.FontNotice, Does.Contain("PREAMBLE\n"));
            Assert.That(Attribution.FontNotice, Does.Not.Contain("PREAMBLE The goals"));
        });
    }
}
