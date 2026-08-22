using Lemmy.Domain;
using Lemmy.Services;

namespace Lemmy.Tests.Session;

[TestFixture]
internal sealed class SessionStoreTests
{
    private static StoredSession Sample(string account = "alice", string instance = "lemmy.world") =>
        new(InstanceAddress.Parse(instance), new SessionToken("token-value-for-tests"), new Username(account));

    [Test]
    public void TheFormatRoundTrips()
    {
        StoredSession original = Sample();

        Assert.That(SessionFormat.TryRead(SessionFormat.Write(original), out StoredSession read), Is.True);
        Assert.That(read, Is.EqualTo(original));
    }

    [TestCase("")]
    [TestCase("nonsense")]
    [TestCase("1\ttoo\tfew")]
    [TestCase("9\nlemmy.world\nalice\ntoken")]
    [TestCase("1\nnot a host\nalice\ntoken")]
    [TestCase("1\nlemmy.world\nalice\n")]
    public void MalformedStorageIsRejectedRatherThanGuessedAt(string stored) =>
        Assert.That(SessionFormat.TryRead(stored, out _), Is.False);

    /// <summary>
    /// A token that prints itself ends up in a log. The type refuses to help.
    /// </summary>
    [Test]
    public void ATokenDoesNotPrintItself()
    {
        var token = new SessionToken("super-secret-value");

        Assert.Multiple(() =>
        {
            Assert.That(token.ToString(), Does.Not.Contain("super-secret-value"));
            Assert.That($"{token}", Does.Not.Contain("super-secret-value"));
            Assert.That(token.Value, Is.EqualTo("super-secret-value"), "it is still readable when asked directly");
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("has space")]
    [TestCase("has\nnewline")]
    public void AnUnusableTokenIsRejected(string value) =>
        Assert.That(SessionToken.TryCreate(value, out _), Is.False);

    [Test]
    public void ADefaultTokenIsSignedOut() =>
        Assert.That(default(SessionToken).IsValid, Is.False);

    [Test]
    public async Task TheMemoryStoreForgetsNothingWhileRunningAndKeepsNothingAfter()
    {
        var store = new MemorySessionStore();

        Assert.That(await store.LoadAsync(), Is.Null);

        await store.SaveAsync(Sample());
        Assert.That((await store.LoadAsync())?.AccountName.Value, Is.EqualTo("alice"));

        await store.ClearAsync();

        Assert.Multiple(() =>
        {
            Assert.That(store.LoadAsync().Result, Is.Null);
            Assert.That(store.IsPersistent, Is.False, "a fresh run starts signed out");
        });
    }

    /// <summary>
    /// The fallback is memory, never a plain file. A token on disk in the clear is exactly what a
    /// swept-up home directory or a restored backup hands to someone.
    /// </summary>
    [Test]
    public void TheDefaultStoreNeverFallsBackToAPlainFile()
    {
        ISessionStore store = SessionStores.CreateDefault();

        Assert.That(
            store,
            Is.InstanceOf<SecretServiceSessionStore>()
                .Or.InstanceOf<DataProtectionSessionStore>()
                .Or.InstanceOf<MemorySessionStore>());
    }

    [Test]
    public void EveryStoreSaysWhatItDoesWithTheToken()
    {
        ISessionStore store = SessionStores.CreateDefault();

        Assert.That(store.Description, Is.Not.Empty);
    }

    /// <summary>
    /// Runs against the real keychain when this machine has one, so the interop is exercised rather
    /// than assumed. Cleans up after itself either way.
    /// </summary>
    [Test]
    public async Task TheDesktopKeychainRoundTripsWhenThereIsOne()
    {
        if (!SecretServiceSessionStore.IsAvailable())
        {
            Assert.Ignore("no Secret Service on this machine");
        }

        var store = new SecretServiceSessionStore();
        StoredSession? before = await store.LoadAsync();

        try
        {
            await store.SaveAsync(Sample("probe-account", "lemmy.world"));
            StoredSession? loaded = await store.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(loaded?.AccountName.Value, Is.EqualTo("probe-account"));
                Assert.That(loaded?.Token.Value, Is.EqualTo("token-value-for-tests"));
            });

            await store.ClearAsync();
            Assert.That(await store.LoadAsync(), Is.Null);
        }
        finally
        {
            // Leave the reader's own keychain as it was found.
            if (before is { } original)
            {
                await store.SaveAsync(original);
            }
            else
            {
                await store.ClearAsync();
            }
        }
    }
}
