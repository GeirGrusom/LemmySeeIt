namespace Lemmy.Services;

/// <summary>
/// Keeps the session for as long as the app is running and no longer. Used where the platform has
/// no keychain: signing in again next launch is a smaller cost than leaving a working token in a
/// file that any backup would sweep up.
/// </summary>
public sealed class MemorySessionStore : ISessionStore
{
    private StoredSession? session;

    /// <inheritdoc />
    public string Description => "kept only until the app closes";

    /// <inheritdoc />
    public bool IsPersistent => false;

    /// <inheritdoc />
    public Task<StoredSession?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(session);

    /// <inheritdoc />
    public Task SaveAsync(StoredSession session, CancellationToken cancellationToken = default)
    {
        this.session = session;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        session = null;
        return Task.CompletedTask;
    }
}
