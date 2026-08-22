using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Services;

/// <summary>A signed-in session, as it is remembered between runs.</summary>
/// <param name="Instance">Which server it belongs to. A token is worthless anywhere else.</param>
/// <param name="Token">The bearer token.</param>
/// <param name="AccountName">Who it belongs to, so the account can be named before the network answers.</param>
public readonly record struct StoredSession(InstanceAddress Instance, SessionToken Token, Username AccountName)
{
    /// <summary>Whether this describes a usable session.</summary>
    public bool IsValid => Instance.IsValid && Token.IsValid && AccountName.IsValid;
}

/// <summary>
/// Keeps the session token somewhere it survives a restart.
/// </summary>
/// <remarks>
/// The bar every implementation has to clear: a token must never sit unencrypted on disk, so that
/// someone who walks off with a backup, a disk image or a directory of dotfiles gets nothing usable.
/// Defending against code already running as the reader is explicitly not the goal — no desktop can
/// — which is why the platform's own keychain is the answer rather than anything home-made. Where
/// there is no keychain, <see cref="MemorySessionStore"/> keeps nothing at all and the reader signs
/// in again next launch, which is better than a file that only looks protected.
/// </remarks>
public interface ISessionStore
{
    /// <summary>What this store does with the token, in words a reader could be shown.</summary>
    string Description { get; }

    /// <summary>Whether the session survives a restart at all.</summary>
    bool IsPersistent { get; }

    /// <summary>Reads the stored session, or <see langword="null"/> when there is none.</summary>
    Task<StoredSession?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores a session, replacing whatever was there.</summary>
    Task SaveAsync(StoredSession session, CancellationToken cancellationToken = default);

    /// <summary>Forgets the stored session.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
