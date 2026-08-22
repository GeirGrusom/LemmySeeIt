using Lemmy.Domain;

namespace Lemmy.Tests.Domain;

[TestFixture]
internal sealed class CountTests
{
    [TestCase(0, "0")]
    [TestCase(7, "7")]
    [TestCase(999, "999")]
    [TestCase(1000, "1k")]
    [TestCase(1200, "1.2k")]
    [TestCase(9999, "9.9k")]
    [TestCase(34000, "34k")]
    [TestCase(197152, "197k")]
    [TestCase(1_100_000, "1.1M")]
    [TestCase(6_919_916, "6.9M")]
    [TestCase(2_000_000_000, "2B")]
    public void ToCompactString_ShortensForABadge(int value, string expected) =>
        Assert.That(VoteCount.Clamp(value).ToCompactString(), Is.EqualTo(expected));

    [TestCase(-1200, "-1.2k")]
    [TestCase(-5, "-5")]
    public void Score_ToCompactString_KeepsTheSign(int value, string expected) =>
        Assert.That(new Score(value).ToCompactString(), Is.EqualTo(expected));

    [Test]
    public void Score_IsSignedBecauseADownvotedPostIsARealState()
    {
        var score = Score.FromVotes(new VoteCount(3), new VoteCount(10));

        Assert.Multiple(() =>
        {
            Assert.That(score.Value, Is.EqualTo(-7));
            Assert.That(score.IsNegative, Is.True);
        });
    }

    [Test]
    public void VoteCount_RejectsANegativeCount() =>
        Assert.That(() => new VoteCount(-1), Throws.TypeOf<DomainValidationException>());

    [Test]
    public void VoteCount_Clamp_AcceptsWhatAServerMightSend() =>
        Assert.That(VoteCount.Clamp(-5).Value, Is.Zero);

    [Test]
    public void VoteCount_Compares()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new VoteCount(3) < new VoteCount(4), Is.True);
            Assert.That(new VoteCount(4) >= new VoteCount(4), Is.True);
        });
    }

    /// <summary>int.MinValue has no positive counterpart; the formatter must not overflow on it.</summary>
    [Test]
    public void CompactString_SurvivesIntMinValue() =>
        Assert.That(new Score(int.MinValue).ToCompactString(), Does.StartWith("-2"));
}
