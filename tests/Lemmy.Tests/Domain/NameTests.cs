using Lemmy.Domain;

namespace Lemmy.Tests.Domain;

[TestFixture]
internal sealed class NameTests
{
    [TestCase("technology", "technology")]
    [TestCase("!technology", "technology")]
    [TestCase("!technology@lemmy.world", "technology")]
    [TestCase("technology@lemmy.world", "technology")]
    [TestCase("  lemmy_shitpost  ", "lemmy_shitpost")]
    [TestCase("news2", "news2")]
    public void CommunityName_TryParse_KeepsTheLocalPart(string input, string expected)
    {
        bool parsed = CommunityName.TryParse(input, out CommunityName name);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(name.Value, Is.EqualTo(expected));
        });
    }

    [TestCase("")]
    [TestCase("!")]
    [TestCase("Technology")]
    [TestCase("tech nology")]
    [TestCase("tech-nology")]
    [TestCase("tech.nology")]
    public void CommunityName_TryParse_RejectsAnythingLemmyWouldNot(string input) =>
        Assert.That(CommunityName.TryParse(input, out _), Is.False);

    [Test]
    public void CommunityName_Constructor_ThrowsOnRejection() =>
        Assert.That(() => new CommunityName("Not Valid"), Throws.TypeOf<DomainValidationException>());

    [TestCase("alice", "alice")]
    [TestCase("@alice", "alice")]
    [TestCase("@alice@lemmy.world", "alice")]
    [TestCase("Buage_", "Buage_")]
    public void Username_TryParse_KeepsTheLocalPart(string input, string expected)
    {
        bool parsed = Username.TryParse(input, out Username name);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(name.Value, Is.EqualTo(expected));
        });
    }

    /// <summary>
    /// Usernames federate in from software with looser rules than Lemmy's, so mixed case is fine;
    /// only what would break a URL or a byline is refused.
    /// </summary>
    [TestCase("")]
    [TestCase("@")]
    [TestCase("ali ce")]
    [TestCase("ali/ce")]
    public void Username_TryParse_RejectsOnlyWhatWouldBreakSomething(string input) =>
        Assert.That(Username.TryParse(input, out _), Is.False);

    [Test]
    public void Username_TooLong_IsRejected() =>
        Assert.That(Username.TryParse(new string('a', 61), out _), Is.False);
}
