using System.Globalization;

namespace Lemmy.Domain;

/// <summary>
/// Upvotes minus downvotes. Unlike <see cref="VoteCount"/> this is signed, because a well-downvoted
/// post is a real and meaningful state rather than a data error.
/// </summary>
public readonly record struct Score : ISpanFormattable, IComparable<Score>
{
    /// <summary>Wraps a raw score.</summary>
    public Score(int value) => Value = value;

    /// <summary>The raw score.</summary>
    public int Value { get; }

    /// <summary><see langword="true"/> when the community has voted this down on balance.</summary>
    public bool IsNegative => Value < 0;

    /// <summary>Derives the score from a vote split.</summary>
    public static Score FromVotes(VoteCount upvotes, VoteCount downvotes) => new(upvotes.Value - downvotes.Value);

    /// <summary>Renders the score compactly for a badge, e.g. <c>-1.2k</c>.</summary>
    public string ToCompactString() => CompactNumber.Format(Value);

    /// <inheritdoc />
    public int CompareTo(Score other) => Value.CompareTo(other.Value);

    /// <summary>Orders by the raw score.</summary>
    public static bool operator <(Score left, Score right) => left.CompareTo(right) < 0;

    /// <summary>Orders by the raw score.</summary>
    public static bool operator >(Score left, Score right) => left.CompareTo(right) > 0;

    /// <summary>Orders by the raw score.</summary>
    public static bool operator <=(Score left, Score right) => left.CompareTo(right) <= 0;

    /// <summary>Orders by the raw score.</summary>
    public static bool operator >=(Score left, Score right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);

    /// <summary>Writes the score into <paramref name="destination"/> without allocating.</summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Value.TryFormat(destination, out charsWritten, format, provider);
}
