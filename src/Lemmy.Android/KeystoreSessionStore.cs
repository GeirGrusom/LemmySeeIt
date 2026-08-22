using System.Text;
using Android.Content;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using Lemmy.Services;

namespace Lemmy.Android;

/// <summary>
/// Stores the session encrypted with a key held in the Android Keystore.
/// </summary>
/// <remarks>
/// App-private storage already keeps other apps out, but it does not survive a device image, an
/// <c>adb backup</c> or a cloud backup — all of which are exactly the bulk grab worth defending
/// against. The key here is generated inside the Keystore and is not exportable: on hardware with a
/// secure element it never exists in normal memory at all. So what leaves the device is ciphertext
/// with no key to go with it.
/// </remarks>
internal sealed class KeystoreSessionStore : ISessionStore
{
    private const string KeystoreName = "AndroidKeyStore";
    private const string KeyAlias = "lemmyseeit.session";
    private const string Transformation = "AES/GCM/NoPadding";
    private const string PreferencesName = "session";
    private const string PayloadKey = "payload";

    /// <summary>Recommended by the AES-GCM specification and by the Keystore documentation.</summary>
    private const int NonceLengthInBytes = 12;
    private const int TagLengthInBits = 128;

    private readonly Context context;

    internal KeystoreSessionStore(Context context) => this.context = context;

    /// <inheritdoc />
    public string Description => "encrypted with a key held in this device's keystore";

    /// <inheritdoc />
    public bool IsPersistent => true;

    /// <inheritdoc />
    public Task<StoredSession?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using ISharedPreferences? preferences = Preferences();
            string? payload = preferences?.GetString(PayloadKey, null);

            if (string.IsNullOrEmpty(payload) || FindKey() is not { } key)
            {
                return Task.FromResult<StoredSession?>(null);
            }

            byte[] combined = Convert.FromBase64String(payload);
            if (combined.Length <= NonceLengthInBytes)
            {
                return Task.FromResult<StoredSession?>(null);
            }

            byte[] nonce = combined[..NonceLengthInBytes];
            byte[] ciphertext = combined[NonceLengthInBytes..];

            using Cipher cipher = Cipher.GetInstance(Transformation)!;
            cipher.Init(CipherMode.DecryptMode, key, new GCMParameterSpec(TagLengthInBits, nonce));

            byte[]? plain = cipher.DoFinal(ciphertext);
            if (plain is null)
            {
                return Task.FromResult<StoredSession?>(null);
            }

            string text = Encoding.UTF8.GetString(plain);
            Array.Clear(plain);

            return Task.FromResult(SessionFormat.TryRead(text, out StoredSession session) ? session : (StoredSession?)null);
        }
        catch (Exception exception) when (exception is Java.Lang.Exception or FormatException)
        {
            // A key that has gone — a restored backup, a changed lock screen — means the session is
            // gone with it. Signing in again is the answer, not a crash on launch.
            return Task.FromResult<StoredSession?>(null);
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(StoredSession session, CancellationToken cancellationToken = default)
    {
        try
        {
            IKey key = FindKey() ?? CreateKey();

            using Cipher cipher = Cipher.GetInstance(Transformation)!;
            cipher.Init(CipherMode.EncryptMode, key);

            byte[] nonce = cipher.GetIV() ?? [];
            byte[] plain = Encoding.UTF8.GetBytes(SessionFormat.Write(session));
            byte[]? ciphertext = cipher.DoFinal(plain);
            Array.Clear(plain);

            if (ciphertext is null)
            {
                return Task.CompletedTask;
            }

            byte[] combined = [.. nonce, .. ciphertext];

            using ISharedPreferences? preferences = Preferences();
            using ISharedPreferencesEditor? editor = preferences?.Edit();
            editor?.PutString(PayloadKey, Convert.ToBase64String(combined));
            editor?.Apply();
        }
        catch (Java.Lang.Exception)
        {
            // Failing to persist costs the reader a sign-in next launch, which is survivable.
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using ISharedPreferences? preferences = Preferences();
            using ISharedPreferencesEditor? editor = preferences?.Edit();
            editor?.Remove(PayloadKey);
            editor?.Apply();

            using KeyStore? keystore = OpenKeystore();
            keystore?.DeleteEntry(KeyAlias);
        }
        catch (Java.Lang.Exception)
        {
            // Same again: the caller is discarding the session regardless.
        }

        return Task.CompletedTask;
    }

    private ISharedPreferences? Preferences() =>
        context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);

    private static KeyStore? OpenKeystore()
    {
        KeyStore? keystore = KeyStore.GetInstance(KeystoreName);
        keystore?.Load(null);

        return keystore;
    }

    private static IKey? FindKey()
    {
        using KeyStore? keystore = OpenKeystore();

        return keystore?.GetKey(KeyAlias, null);
    }

    /// <summary>
    /// Generates the key inside the Keystore. It is never handed out, so nothing this app writes
    /// can be decrypted anywhere but on this device.
    /// </summary>
    private static IKey CreateKey()
    {
        using KeyGenParameterSpec.Builder builder = new(
            KeyAlias,
            KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt);

        using KeyGenParameterSpec specification = builder
            .SetBlockModes(KeyProperties.BlockModeGcm!)!
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone!)!
            .SetKeySize(256)!
            .Build()!;

        using KeyGenerator generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes!, KeystoreName)!;
        generator.Init(specification);

        return generator.GenerateKey()!;
    }
}
