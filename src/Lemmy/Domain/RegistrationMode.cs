namespace Lemmy.Domain;

/// <summary>Whether an instance is taking new accounts, and on what terms.</summary>
public enum RegistrationMode
{
    /// <summary>The instance did not say, or said something this client does not recognise.</summary>
    Unknown,

    /// <summary>Anyone can sign up.</summary>
    Open,

    /// <summary>Sign-ups are accepted but an admin has to approve each one.</summary>
    RequireApplication,

    /// <summary>The instance is not taking new accounts.</summary>
    Closed,
}

/// <summary>Reads the registration mode Lemmy sends as a string.</summary>
public static class RegistrationModeExtensions
{
    /// <summary>Maps the wire value.</summary>
    public static RegistrationMode ToRegistrationMode(this string? value) => value switch
    {
        "Open" => RegistrationMode.Open,
        "RequireApplication" => RegistrationMode.RequireApplication,
        "Closed" => RegistrationMode.Closed,
        _ => RegistrationMode.Unknown,
    };

    /// <summary>
    /// Whether it is worth offering to sign up. An unrecognised mode counts as worth trying: a newer
    /// Lemmy could name a mode this client has never heard of, and refusing to show the link would
    /// be worse than showing one that turns out to be shut.
    /// </summary>
    public static bool AcceptsNewAccounts(this RegistrationMode mode) => mode != RegistrationMode.Closed;
}
