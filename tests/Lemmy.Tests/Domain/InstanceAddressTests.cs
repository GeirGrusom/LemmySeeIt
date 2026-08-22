using Lemmy.Domain;

namespace Lemmy.Tests.Domain;

[TestFixture]
internal sealed class InstanceAddressTests
{
    [TestCase("lemmy.world", "lemmy.world")]
    [TestCase("  lemmy.world  ", "lemmy.world")]
    [TestCase("LEMMY.WORLD", "lemmy.world")]
    [TestCase("https://lemmy.world", "lemmy.world")]
    [TestCase("http://lemmy.world/", "lemmy.world")]
    [TestCase("https://lemmy.world/c/technology", "lemmy.world")]
    [TestCase("https://lemmy.world/?sort=New", "lemmy.world")]
    [TestCase("sh.itjust.works", "sh.itjust.works")]
    [TestCase("lemmy.example.com:8536", "lemmy.example.com:8536")]
    public void TryParse_AcceptsWhatSomeoneIsLikelyToPaste(string input, string expected)
    {
        bool parsed = InstanceAddress.TryParse(input, out InstanceAddress address);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(address.Value, Is.EqualTo(expected));
        });
    }

    /// <summary>A fully-qualified name pastes in often enough to be worth handling.</summary>
    [TestCase("!technology@lemmy.world", "lemmy.world")]
    [TestCase("@alice@sh.itjust.works", "sh.itjust.works")]
    public void TryParse_KeepsTheInstanceOutOfAQualifiedName(string input, string expected)
    {
        InstanceAddress.TryParse(input, out InstanceAddress address);

        Assert.That(address.Value, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("localhost")]
    [TestCase(".lemmy.world")]
    [TestCase("lemmy.world.")]
    [TestCase("-lemmy.world")]
    [TestCase("lemmy..world")]
    [TestCase("lemmy world.com")]
    [TestCase("lemmy_world.com")]
    public void TryParse_RejectsAnythingThatIsNotAHost(string input) =>
        Assert.That(InstanceAddress.TryParse(input, out _), Is.False);

    [Test]
    public void Parse_ThrowsOnRejection() =>
        Assert.That(() => InstanceAddress.Parse("not a host"), Throws.TypeOf<DomainValidationException>());

    [Test]
    public void BaseUri_IsAlwaysHttpsAndAlwaysRooted() =>
        Assert.That(InstanceAddress.Parse("lemmy.world").BaseUri, Is.EqualTo(new Uri("https://lemmy.world/")));

    [Test]
    public void Default_IsNotValid()
    {
        Assert.Multiple(() =>
        {
            Assert.That(default(InstanceAddress).IsValid, Is.False);
            Assert.That(default(InstanceAddress).Value, Is.Empty);
        });
    }

    [Test]
    public void Equality_IgnoresTheCaseAndFormThatWasTypedIn() =>
        Assert.That(InstanceAddress.Parse("HTTPS://Lemmy.World/c/x"), Is.EqualTo(InstanceAddress.Parse("lemmy.world")));
}
