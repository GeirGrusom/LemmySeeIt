using System.Security.Cryptography;
using System.Text;

namespace Lemmy.Services;

/// <summary>
/// Stores the session on Windows, encrypted with DPAPI under the signed-in Windows account.
/// </summary>
/// <remarks>
/// A file, unlike the other stores — but an encrypted one whose key belongs to the Windows account
/// and never leaves the machine, so a copied disk, a backup or a swept-up profile directory yields
/// nothing usable. The Credential Manager would avoid writing a file at all and is the better
/// long-term home; it needs a block of struct-marshalling interop that cannot be exercised from
/// here, and shipping unverifiable native interop is worse than shipping this.
/// </remarks>
public sealed class DataProtectionSessionStore : ISessionStore
{
    private const string FolderName = "LemmySeeIt";
    private const string FileName = "session.bin";

    private readonly string filePath;

    /// <summary>Uses the platform's per-user application data location.</summary>
    public DataProtectionSessionStore()
        : this(DefaultPath())
    {
    }

    /// <summary>Uses an explicit path, which is what the tests do.</summary>
    public DataProtectionSessionStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
    }

    /// <inheritdoc />
    public string Description => "encrypted for your Windows account";

    /// <inheritdoc />
    public bool IsPersistent => true;

    /// <summary>Whether this platform can protect data this way at all.</summary>
    public static bool IsAvailable() => OperatingSystem.IsWindows();

    /// <inheritdoc />
    public async Task<StoredSession?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            byte[] protectedBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            byte[] plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);

            return SessionFormat.TryRead(Encoding.UTF8.GetString(plain), out StoredSession session) ? session : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            // A session that cannot be decrypted — a different account, a restored profile — is a
            // session that is gone. Signing in again is the answer, not an error.
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(StoredSession session, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        byte[] plain = Encoding.UTF8.GetBytes(SessionFormat.Write(session));
        byte[] protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        Array.Clear(plain);

        await File.WriteAllBytesAsync(filePath, protectedBytes, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing useful to do; the caller is discarding the session either way.
        }

        return Task.CompletedTask;
    }

    private static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
            FolderName,
            FileName);
}
