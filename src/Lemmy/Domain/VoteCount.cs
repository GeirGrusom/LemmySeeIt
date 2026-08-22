using System.Globalization;

namespace Lemmy.Domain;

/// <summary>A tally of votes or comments. Never negative — subtraction is what <see cref="Score"/> is for.</summary>
public readonly record struct VoteCount : ISpanFormattable, IComparable<VoteCount>
{
    /// <summary>Wraps a raw count.</summary>
    /// <exception cref="DomainValidationException">The count is negative.</exception>
    public VoteCount(int value)
    {
        if (value < 0)
        {
            throw DomainValidationException.For(
                nameof(VoteCount),
                $"counts cannot be negative but was {value.ToString(CultureInfo.InvariantCulture)}");
        }

        Value = value;
    }

    /// <summary>The raw count.</summary>
    public int Value { get; }

    /// <summary>Wraps a raw count without throwing; negatives clamp to zero.</summary>
    public static bool TryCreate(int value, out VoteCount count)
    {
        count = value < 0 ? default : new VoteCount(value);
        return value >= 0;
    }

    /// <summary>Clamps a possibly-negative wire value to a usable count.</summary>
    public static VoteCount Clamp(int value) => new(Math.Max(0, value));

    /// <summary>Renders the count compactly for a badge, e.g. <c>1.2k</c>.</summary>
    public string ToCompactString() => CompactNumber.Format(Value);

    /// <inheritdoc />
    public int CompareTo(VoteCount other) => Value.CompareTo(other.Value);

    /// <summary>Orders by the raw count.</summary>
    public static bool operator <(VoteCount left, VoteCount right) => left.CompareTo(right) < 0;

    /// <summary>Orders by the raw count.</summary>
    public static bool operator >(VoteCount left, VoteCount right) => left.CompareTo(right) > 0;

    /// <summary>Orders by the raw count.</summary>
    public static bool operator <=(VoteCount left, VoteCount right) => left.CompareTo(right) <= 0;

    /// <summary>Orders by the raw count.</summary>
    public static bool operator >=(VoteCount left, VoteCount right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);

    /// <summary>Writes the count into <paramref name="destination"/> without allocating.</summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Value.TryFormat(destination, out charsWritten, format, provider);
}
