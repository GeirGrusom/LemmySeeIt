using System.Globalization;

namespace Lemmy.Domain;

/// <summary>
/// How many levels of replies to fetch in one go. Deep threads are the norm on Lemmy, so this is
/// the knob that decides between "one round trip" and "a spinner every time you expand something".
/// </summary>
public readonly record struct CommentDepth : ISpanFormattable
{
    /// <summary>Fetching at least one level is the minimum that returns anything.</summary>
    public const int Minimum = 1;

    /// <summary>Beyond this a server starts refusing, and the payload stops being worth it anyway.</summary>
    public const int Maximum = 50;

    private const int DefaultDepth = 8;

    private readonly int depth;

    /// <summary>Validates a depth.</summary>
    /// <exception cref="DomainValidationException">The depth is outside <see cref="Minimum"/>..<see cref="Maximum"/>.</exception>
    public CommentDepth(int value)
    {
        if (value is < Minimum or > Maximum)
        {
            throw DomainValidationException.For(
                nameof(CommentDepth),
                $"the depth must be {Minimum}-{Maximum} but was {value.ToString(CultureInfo.InvariantCulture)}");
        }

        depth = value;
    }

    /// <summary>The depth a post view uses when nothing else is specified.</summary>
    public static CommentDepth Default => default;

    /// <summary>The depth, resolving <see langword="default"/> to the app's own default.</summary>
    public int Value => depth == 0 ? DefaultDepth : depth;

    /// <summary>Clamps any number into the range a server will accept.</summary>
    public static CommentDepth Clamp(int value) => new(Math.Clamp(value, Minimum, Maximum));

    /// <summary>Validates a depth without throwing.</summary>
    public static bool TryCreate(int value, out CommentDepth commentDepth)
    {
        bool valid = value is >= Minimum and <= Maximum;
        commentDepth = valid ? new CommentDepth(value) : default;
        return valid;
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);

    /// <summary>Writes the depth into <paramref name="destination"/> without allocating.</summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Value.TryFormat(destination, out charsWritten, format, provider);
}
