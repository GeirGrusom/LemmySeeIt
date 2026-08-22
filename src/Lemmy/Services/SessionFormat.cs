using Lemmy.Domain;

namespace Lemmy.Services;

/// <summary>
/// How a session is written into a single secret. Deliberately one opaque string rather than
/// structured storage: every keychain stores one value per entry, and a hand-rolled three-field
/// format is smaller and less to go wrong than JSON for something read once at launch.
/// </summary>
/// <remarks>
/// Public because it is part of the <see cref="ISessionStore"/> contract rather than an
/// implementation detail of any one store: a platform head writing its own store — as the Android
/// one does — has to produce exactly the same bytes.
/// </remarks>
public static class SessionFormat
{
    private const char Separator = '\n';
    private const string Version = "1";

    /// <summary>Renders a session as the single string a keychain entry holds.</summary>
    public static string Write(StoredSession session) =>
        string.Join(Separator, Version, session.Instance.Value, session.AccountName.Value, session.Token.Value);

    /// <summary>Reads a session back, rejecting anything malformed or from an older format.</summary>
    public static bool TryRead(string? stored, out StoredSession session)
    {
        session = default;

        if (string.IsNullOrEmpty(stored))
        {
            return false;
        }

        string[] parts = stored.Split(Separator);
        if (parts.Length != 4 || parts[0] != Version)
        {
            return false;
        }

        if (!InstanceAddress.TryParse(parts[1], out InstanceAddress instance)
            || !Username.TryParse(parts[2], out Username account)
            || !SessionToken.TryCreate(parts[3], out SessionToken token))
        {
            return false;
        }

        session = new StoredSession(instance, token, account);
        return true;
    }
}
