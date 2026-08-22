namespace Lemmy.Services;

/// <summary>Picks the best place this platform has to keep a session token.</summary>
public static class SessionStores
{
    /// <summary>
    /// The keychain where there is one, and nothing at all where there is not.
    /// </summary>
    /// <remarks>
    /// The last resort is memory rather than a file, deliberately. The bar is that a token must
    /// never sit unencrypted on disk; a plain file with tight permissions still hands a working
    /// session to anything that sweeps up a home directory, whereas signing in again next launch
    /// costs the reader a few seconds and gives an attacker nothing.
    /// </remarks>
    public static ISessionStore CreateDefault()
    {
        if (SecretServiceSessionStore.IsAvailable())
        {
            return new SecretServiceSessionStore();
        }

        if (DataProtectionSessionStore.IsAvailable())
        {
            return new DataProtectionSessionStore();
        }

        return new MemorySessionStore();
    }
}
