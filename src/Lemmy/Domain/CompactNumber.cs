using System.Globalization;

namespace Lemmy.Domain;

/// <summary>
/// Shortens counts for the cramped badges on a feed card: <c>987</c>, <c>1.2k</c>, <c>34k</c>,
/// <c>1.1M</c>. Formats through a stack buffer so scrolling a long feed does not churn strings.
/// </summary>
internal static class CompactNumber
{
    internal static string Format(int value)
    {
        Span<char> buffer = stackalloc char[16];
        int written = Format(value, buffer);
        return new string(buffer[..written]);
    }

    internal static int Format(int value, Span<char> destination)
    {
        if (value < 0)
        {
            destination[0] = '-';
            // int.MinValue has no positive counterpart; the nearest representable value reads the same.
            int magnitude = value == int.MinValue ? int.MaxValue : -value;
            return 1 + Format(magnitude, destination[1..]);
        }

        (int divisor, char suffix) = value switch
        {
            >= 1_000_000_000 => (1_000_000_000, 'B'),
            >= 1_000_000 => (1_000_000, 'M'),
            >= 1_000 => (1_000, 'k'),
            _ => (1, '\0'),
        };

        if (suffix == '\0')
        {
            value.TryFormat(destination, out int plainWritten, provider: CultureInfo.InvariantCulture);
            return plainWritten;
        }

        int whole = value / divisor;
        int tenths = value % divisor / (divisor / 10);

        whole.TryFormat(destination, out int written, provider: CultureInfo.InvariantCulture);

        // Only one significant decimal, and only while it still fits in three leading digits.
        if (whole < 10 && tenths > 0)
        {
            destination[written++] = '.';
            tenths.TryFormat(destination[written..], out int tenthsWritten, provider: CultureInfo.InvariantCulture);
            written += tenthsWritten;
        }

        destination[written++] = suffix;
        return written;
    }
}
