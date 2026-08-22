namespace Lemmy.Tests.TestSupport;

/// <summary>
/// A clock that does not move, so anything that formats an age produces the same words on every
/// run. Advancing it is explicit, which is the point: a test that depends on elapsed time should
/// have to say so.
/// </summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset now;

    internal FixedTimeProvider(DateTimeOffset now) => this.now = now;

    /// <summary>An arbitrary but fixed instant, used wherever the exact value does not matter.</summary>
    internal static DateTimeOffset Reference { get; } = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    internal static FixedTimeProvider AtReference() => new(Reference);

    public override DateTimeOffset GetUtcNow() => now;

    internal void Advance(TimeSpan amount) => now += amount;
}
