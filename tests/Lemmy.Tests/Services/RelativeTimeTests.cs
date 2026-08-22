using Lemmy.Services;
using Lemmy.Tests.TestSupport;

namespace Lemmy.Tests.Services;

[TestFixture]
internal sealed class RelativeTimeTests
{
    private static readonly DateTimeOffset Now = FixedTimeProvider.Reference;

    [TestCase(0, "now")]
    [TestCase(30, "now")]
    [TestCase(60, "1m")]
    [TestCase(59 * 60, "59m")]
    [TestCase(60 * 60, "1h")]
    [TestCase(23 * 3600, "23h")]
    [TestCase(24 * 3600, "1d")]
    [TestCase(29 * 24 * 3600, "29d")]
    [TestCase(31 * 24 * 3600, "1mo")]
    [TestCase(200L * 24 * 3600, "6mo")]
    [TestCase(400L * 24 * 3600, "1y")]
    public void Format_ShortensTheAgeToTheLargestUsefulUnit(long secondsAgo, string expected) =>
        Assert.That(RelativeTime.Format(Now.AddSeconds(-secondsAgo), Now), Is.EqualTo(expected));

    /// <summary>
    /// Instances run their own clocks. A post a few seconds "in the future" is skew, not a bug, and
    /// should read as "now" rather than as a negative age.
    /// </summary>
    [Test]
    public void Format_TreatsClockSkewAsNow() =>
        Assert.That(RelativeTime.Format(Now.AddSeconds(30), Now), Is.EqualTo("now"));

    /// <summary>Past a few years, the date says more than the elapsed time does.</summary>
    [Test]
    public void Format_FallsBackToADateOnceTheAgeStopsBeingInformative() =>
        Assert.That(RelativeTime.Format(new DateTimeOffset(2019, 3, 4, 0, 0, 0, TimeSpan.Zero), Now), Is.EqualTo("2019-03-04"));

    [Test]
    public void Format_RoundsDownSoNothingReadsAsOlderThanItIs() =>
        Assert.That(RelativeTime.Format(Now.AddMinutes(-119), Now), Is.EqualTo("1h"));
}
