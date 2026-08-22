namespace Lemmy.Api;

/// <summary>
/// What sign-in needs. Not a domain type and never stored: the password exists for the length of
/// one request and the app keeps only the token the server hands back.
/// </summary>
/// <param name="UsernameOrEmail">Either works; Lemmy accepts both.</param>
/// <param name="Password">Sent once and then forgotten.</param>
/// <param name="TotpToken">The six digits, when the account has two-factor sign-in turned on.</param>
public readonly record struct LoginRequest(string UsernameOrEmail, string Password, string? TotpToken = null)
{
    /// <summary>Whether there is enough here to be worth sending.</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(UsernameOrEmail) && !string.IsNullOrEmpty(Password);
}
