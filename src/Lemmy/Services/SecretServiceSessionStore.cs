using System.Runtime.InteropServices;

namespace Lemmy.Services;

/// <summary>
/// Stores the session in the desktop keychain through libsecret — KWallet under Plasma, GNOME
/// Keyring under GNOME, anything else serving <c>org.freedesktop.secrets</c>.
/// </summary>
/// <remarks>
/// <para>
/// libsecret rather than the Secret Service D-Bus protocol directly: the protocol involves
/// sessions, algorithm negotiation, prompt objects and collection unlocking, and libsecret reduces
/// all of it to a handful of calls.
/// </para>
/// <para>
/// The <c>v</c> variants, not the friendlier ones. <c>secret_password_lookup_sync</c> and friends
/// are C variadic functions, which P/Invoke does not reliably support — on x86-64 a variadic callee
/// reads a register the runtime does not set. The <c>v</c> forms take a hash table of attributes
/// instead, which costs a few more calls into glib and is actually specified to work.
/// </para>
/// </remarks>
public sealed class SecretServiceSessionStore : ISessionStore
{
    private const string Secret = "libsecret-1.so.0";
    private const string Glib = "libglib-2.0.so.0";

    /// <summary>Marks this app's entry so it can be found again and replaced.</summary>
    private const string AttributeName = "application";
    private const string AttributeValue = "lemmyseeit";

    /// <inheritdoc />
    public string Description => "stored in the desktop keychain";

    /// <inheritdoc />
    public bool IsPersistent => true;

    /// <summary>Whether libsecret is present and a keychain actually answers.</summary>
    public static bool IsAvailable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        try
        {
            // A lookup is the cheapest question that exercises the whole path.
            using var attributes = new Attributes();
            IntPtr found = secret_password_lookupv_sync(IntPtr.Zero, attributes.Handle, IntPtr.Zero, out IntPtr error);

            // Finding nothing is a fine answer and leaves no error. An error means the keyring
            // itself could not be reached — the library loaded but there is no service behind it,
            // which is a KDE session whose wallet is shut as readily as a machine without one.
            // Reporting that as available would pick this store and then quietly fail to save.
            bool reachable = error == IntPtr.Zero;

            Release(found, error);
            return reachable;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task<StoredSession?> LoadAsync(CancellationToken cancellationToken = default)
    {
        using var attributes = new Attributes();
        IntPtr found = secret_password_lookupv_sync(IntPtr.Zero, attributes.Handle, IntPtr.Zero, out IntPtr error);

        try
        {
            if (found == IntPtr.Zero)
            {
                return Task.FromResult<StoredSession?>(null);
            }

            string? stored = Marshal.PtrToStringUTF8(found);

            return Task.FromResult(SessionFormat.TryRead(stored, out StoredSession session) ? session : (StoredSession?)null);
        }
        finally
        {
            Release(found, error);
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(StoredSession session, CancellationToken cancellationToken = default)
    {
        using var attributes = new Attributes();

        _ = secret_password_storev_sync(
            IntPtr.Zero,
            attributes.Handle,
            null,
            $"LemmySeeIt — {session.AccountName.Value}@{session.Instance.Value}",
            SessionFormat.Write(session),
            IntPtr.Zero,
            out IntPtr error);

        Release(IntPtr.Zero, error);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        using var attributes = new Attributes();

        _ = secret_password_clearv_sync(IntPtr.Zero, attributes.Handle, IntPtr.Zero, out IntPtr error);

        Release(IntPtr.Zero, error);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Frees what libsecret handed back. The secret goes through <c>secret_password_free</c>, which
    /// wipes the memory rather than merely releasing it.
    /// </summary>
    private static void Release(IntPtr password, IntPtr error)
    {
        if (password != IntPtr.Zero)
        {
            secret_password_free(password);
        }

        if (error != IntPtr.Zero)
        {
            g_error_free(error);
        }
    }

    /// <summary>The attribute table that identifies this app's entry, owned for one call.</summary>
    private sealed class Attributes : IDisposable
    {
        private readonly IntPtr name = Marshal.StringToCoTaskMemUTF8(AttributeName);
        private readonly IntPtr value = Marshal.StringToCoTaskMemUTF8(AttributeValue);

        internal Attributes()
        {
            Handle = g_hash_table_new(g_str_hash_address, g_str_equal_address);
            g_hash_table_insert(Handle, name, value);
        }

        internal IntPtr Handle { get; }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                g_hash_table_destroy(Handle);
            }

            Marshal.FreeCoTaskMem(name);
            Marshal.FreeCoTaskMem(value);
        }
    }

    private static readonly IntPtr g_str_hash_address = NativeLibrary.GetExport(NativeLibrary.Load(Glib), "g_str_hash");
    private static readonly IntPtr g_str_equal_address = NativeLibrary.GetExport(NativeLibrary.Load(Glib), "g_str_equal");

    [DllImport(Secret, EntryPoint = "secret_password_lookupv_sync")]
    private static extern IntPtr secret_password_lookupv_sync(IntPtr schema, IntPtr attributes, IntPtr cancellable, out IntPtr error);

    [DllImport(Secret, EntryPoint = "secret_password_storev_sync", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I4)]
    private static extern int secret_password_storev_sync(
        IntPtr schema,
        IntPtr attributes,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string password,
        IntPtr cancellable,
        out IntPtr error);

    [DllImport(Secret, EntryPoint = "secret_password_clearv_sync")]
    [return: MarshalAs(UnmanagedType.I4)]
    private static extern int secret_password_clearv_sync(IntPtr schema, IntPtr attributes, IntPtr cancellable, out IntPtr error);

    [DllImport(Secret, EntryPoint = "secret_password_free")]
    private static extern void secret_password_free(IntPtr password);

    [DllImport(Glib, EntryPoint = "g_error_free")]
    private static extern void g_error_free(IntPtr error);

    [DllImport(Glib, EntryPoint = "g_hash_table_new")]
    private static extern IntPtr g_hash_table_new(IntPtr hashFunc, IntPtr equalFunc);

    [DllImport(Glib, EntryPoint = "g_hash_table_insert")]
    [return: MarshalAs(UnmanagedType.I4)]
    private static extern int g_hash_table_insert(IntPtr table, IntPtr key, IntPtr value);

    [DllImport(Glib, EntryPoint = "g_hash_table_destroy")]
    private static extern void g_hash_table_destroy(IntPtr table);
}
