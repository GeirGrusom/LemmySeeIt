using System.Collections.Immutable;
using Lemmy.Domain;
using Lemmy.Services;

namespace Lemmy.Tests.Services;

/// <summary>Remembering the servers the reader has actually used.</summary>
[TestFixture]
internal sealed class RememberedInstanceTests
{
    private static InstanceAddress Address(string value) => InstanceAddress.Parse(value);

    [Test]
    public void MovingToAServerPutsItAtTheFrontAndSelectsIt()
    {
        AppSettings settings = AppSettings.Default.WithInstance(Address("lemmy.ml"));

        Assert.Multiple(() =>
        {
            Assert.That(settings.Instance, Is.EqualTo(Address("lemmy.ml")));
            Assert.That(settings.Recent[0], Is.EqualTo(Address("lemmy.ml")));
        });
    }

    [Test]
    public void GoingBackToAServerPromotesItRatherThanRepeatingIt()
    {
        AppSettings settings = AppSettings.Default
            .WithInstance(Address("lemmy.ml"))
            .WithInstance(Address("sopuli.xyz"))
            .WithInstance(Address("lemmy.ml"));

        Assert.Multiple(() =>
        {
            Assert.That(settings.Recent[0], Is.EqualTo(Address("lemmy.ml")));
            Assert.That(settings.Recent, Is.Unique);
            Assert.That(settings.Recent, Has.Length.EqualTo(2));
        });
    }

    [Test]
    public void OnlySoManyServersAreRemembered()
    {
        AppSettings settings = AppSettings.Default;
        for (int index = 0; index < AppSettings.MaxRecentInstances + 4; index++)
        {
            settings = settings.WithInstance(Address($"instance{index}.example"));
        }

        Assert.That(settings.Recent, Has.Length.EqualTo(AppSettings.MaxRecentInstances));
    }

    [Test]
    public void TheOldestIsTheOneForgotten()
    {
        AppSettings settings = AppSettings.Default.WithInstance(Address("first.example"));
        for (int index = 0; index < AppSettings.MaxRecentInstances; index++)
        {
            settings = settings.WithInstance(Address($"later{index}.example"));
        }

        Assert.That(settings.Recent, Does.Not.Contain(Address("first.example")));
    }

    [Test]
    public void AnInvalidAddressChangesNothing()
    {
        AppSettings before = AppSettings.Default.WithInstance(Address("lemmy.ml"));

        Assert.That(before.WithInstance(default), Is.EqualTo(before));
    }

    [Test]
    public void DefaultSettingsHaveAnEmptyRatherThanUninitialisedList() =>
        Assert.That(AppSettings.Default.Recent, Is.Empty);

    /// <summary>
    /// A record compares an <see cref="ImmutableArray{T}"/> member by the array behind it, so this
    /// needs its own equality; without it a saved and reloaded settings file compares unequal.
    /// </summary>
    [Test]
    public void SettingsHoldingTheSameServersAreEqual()
    {
        AppSettings one = AppSettings.Default.WithInstance(Address("lemmy.ml")).WithInstance(Address("sopuli.xyz"));
        AppSettings two = AppSettings.Default.WithInstance(Address("lemmy.ml")).WithInstance(Address("sopuli.xyz"));

        Assert.Multiple(() =>
        {
            Assert.That(one, Is.EqualTo(two));
            Assert.That(one.GetHashCode(), Is.EqualTo(two.GetHashCode()));
        });
    }

    [Test]
    public void SettingsHoldingDifferentServersAreNotEqual()
    {
        AppSettings one = AppSettings.Default.WithInstance(Address("lemmy.ml"));
        AppSettings two = AppSettings.Default.WithInstance(Address("sopuli.xyz"));

        Assert.That(one, Is.Not.EqualTo(two));
    }

    [Test]
    public void EverySuggestedServerIsAUsableAddress()
    {
        Assert.That(KnownInstances.Suggested, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (InstanceAddress address in KnownInstances.Suggested)
            {
                Assert.That(address.IsValid, Is.True, address.Value);
            }

            Assert.That(KnownInstances.Suggested, Is.Unique);
            Assert.That(KnownInstances.Suggested, Does.Contain(AppSettings.Default.Instance));
        });
    }
}
