using System.Globalization;

namespace Lemmy.Services;

/// <summary>
/// Renders timestamps the way a feed wants them: <c>now</c>, <c>7m</c>, <c>3h</c>, <c>2d</c>,
/// <c>5mo</c>, <c>3y</c>. Formats into a stack buffer because a scrolling feed re-renders these
/// constantly, and falls back to an absolute date once "how long ago" stops being informative.
/// </summary>
public static class RelativeTime
{
    private const int MaxLength = 16;

    /// <summary>Beyond this the age matters less than the date, so the date is shown instead.</summary>
    private static readonly TimeSpan AbsoluteThreshold = TimeSpan.FromDays(365 * 3);

    /// <summary>Formats how long ago <paramref name="timestamp"/> was, relative to <paramref name="now"/>.</summary>
    public static string Format(DateTimeOffset timestamp, DateTimeOffset now)
    {
        TimeSpan age = now - timestamp;

        // Clock skew between us and the instance regularly produces "in 4 seconds"; show "now".
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age >= AbsoluteThreshold)
        {
            return timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        Span<char> buffer = stackalloc char[MaxLength];
        int written = FormatCore(age, buffer);

        return new string(buffer[..written]);
    }

    private static int FormatCore(TimeSpan age, Span<char> destination)
    {
        (double amount, string unit) = age switch
        {
            { TotalSeconds: < 45 } => (0, "now"),
            { TotalMinutes: < 60 } => (age.TotalMinutes, "m"),
            { TotalHours: < 24 } => (age.TotalHours, "h"),
            { TotalDays: < 30 } => (age.TotalDays, "d"),
            { TotalDays: < 365 } => (age.TotalDays / 30, "mo"),
            _ => (age.TotalDays / 365, "y"),
        };

        if (amount == 0)
        {
            unit.CopyTo(destination);
            return unit.Length;
        }

        int rounded = Math.Max(1, (int)amount);
        rounded.TryFormat(destination, out int written, provider: CultureInfo.InvariantCulture);
        unit.CopyTo(destination[written..]);

        return written + unit.Length;
    }
}
