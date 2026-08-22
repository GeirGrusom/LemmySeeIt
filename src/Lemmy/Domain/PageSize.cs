using System.Globalization;

namespace Lemmy.Domain;

/// <summary>
/// How many items to ask for in one request. Lemmy rejects anything above 50, and a request for
/// zero silently returns the server default, so both ends are worth pinning down before we ask.
/// </summary>
public readonly record struct PageSize : ISpanFormattable
{
    /// <summary>The smallest page a server will serve.</summary>
    public const int Minimum = 1;

    /// <summary>The largest page a Lemmy server will serve.</summary>
    public const int Maximum = 50;

    private const int DefaultSize = 20;

    private readonly int size;

    /// <summary>Validates a page size.</summary>
    /// <exception cref="DomainValidationException">The size is outside <see cref="Minimum"/>..<see cref="Maximum"/>.</exception>
    public PageSize(int value)
    {
        if (value is < Minimum or > Maximum)
        {
            throw DomainValidationException.For(
                nameof(PageSize),
                $"the page size must be {Minimum}-{Maximum} but was {value.ToString(CultureInfo.InvariantCulture)}");
        }

        size = value;
    }

    /// <summary>The size a feed uses when nothing else is specified.</summary>
    public static PageSize Default => default;

    /// <summary>The page size, resolving <see langword="default"/> to the app's own default.</summary>
    public int Value => size == 0 ? DefaultSize : size;

    /// <summary>Clamps any number into the range a server will accept.</summary>
    public static PageSize Clamp(int value) => new(Math.Clamp(value, Minimum, Maximum));

    /// <summary>Validates a page size without throwing.</summary>
    public static bool TryCreate(int value, out PageSize pageSize)
    {
        bool valid = value is >= Minimum and <= Maximum;
        pageSize = valid ? new PageSize(value) : default;
        return valid;
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);

    /// <summary>Writes the page size into <paramref name="destination"/> without allocating.</summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Value.TryFormat(destination, out charsWritten, format, provider);
}
