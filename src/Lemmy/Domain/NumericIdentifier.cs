using System.Globalization;

namespace Lemmy.Domain;

/// <summary>
/// Implementation helpers shared by every <see cref="IIdentifier{TSelf}"/>. Kept internal so the
/// identifier types stay the only public surface.
/// </summary>
internal static class NumericIdentifier
{
    /// <summary>Lemmy identifiers are database primary keys, so they start at one.</summary>
    internal static bool IsInRange(int value) => value > 0;

    internal static int Validate(int value, string typeName) =>
        IsInRange(value)
            ? value
            : throw DomainValidationException.For(typeName, $"identifiers must be positive but was {value.ToString(CultureInfo.InvariantCulture)}");

    internal static bool TryParse<TSelf>(ReadOnlySpan<char> text, out TSelf identifier)
        where TSelf : struct, IIdentifier<TSelf>
    {
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
        {
            return TSelf.TryCreate(value, out identifier);
        }

        identifier = default;
        return false;
    }

    internal static TSelf Parse<TSelf>(ReadOnlySpan<char> text, string typeName)
        where TSelf : struct, IIdentifier<TSelf>
    {
        return TryParse(text, out TSelf identifier)
            ? identifier
            : throw DomainValidationException.For(typeName, $"'{text.ToString()}' is not a positive integer");
    }
}
