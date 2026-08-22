namespace Lemmy.Domain;

/// <summary>
/// Thrown when a value rejected by a domain type is passed to a throwing factory.
/// Every domain type also exposes a non-throwing <c>TryCreate</c> / <c>TryParse</c> pair,
/// which is the preferred path for anything that came off the wire or out of a text box.
/// </summary>
public sealed class DomainValidationException : Exception
{
    public DomainValidationException()
        : base("A domain value failed validation.")
    {
    }

    public DomainValidationException(string message)
        : base(message)
    {
    }

    public DomainValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal static DomainValidationException For(string typeName, string reason) =>
        new($"'{typeName}' rejected the supplied value: {reason}.");
}
