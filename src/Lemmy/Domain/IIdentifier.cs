namespace Lemmy.Domain;

/// <summary>
/// Shared shape of the integer identifiers Lemmy hands out (post, comment, community, ...).
/// Implemented by <c>readonly record struct</c>s so an identifier is never confused with a
/// bare <see cref="int"/> and never confused with an identifier of a different kind.
/// </summary>
/// <remarks>
/// <see cref="ISpanFormattable"/> is part of the contract on purpose: it lets query strings be
/// composed straight into a stack buffer without allocating an intermediate string per value.
/// </remarks>
public interface IIdentifier<TSelf> : ISpanParsable<TSelf>, ISpanFormattable, IEquatable<TSelf>
    where TSelf : struct, IIdentifier<TSelf>
{
    /// <summary>The underlying instance-local number.</summary>
    int Value { get; }

    /// <summary><see langword="false"/> for <see langword="default"/>, which no instance ever hands out.</summary>
    bool IsValid { get; }

    /// <summary>Creates the identifier, or returns <see langword="false"/> without throwing.</summary>
    static abstract bool TryCreate(int value, out TSelf identifier);
}
