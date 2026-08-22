using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Services;

/// <summary>
/// Who is signed in, for the parts of the app that need to know without being handed it.
/// </summary>
/// <remarks>
/// A comment has to decide whether it belongs to the reader, and comments are built several layers
/// below the shell that did the signing in. Threading the account through every page and row to
/// answer one question would be worse than a small holder the rows can ask.
/// </remarks>
public sealed class CurrentAccount
{
    /// <summary>Raised when somebody signs in or out.</summary>
    public event EventHandler? Changed;

    /// <summary>The signed-in account, or <see langword="null"/>.</summary>
    public Account? Value { get; private set; }

    /// <summary>Records who is signed in; <see langword="null"/> for nobody.</summary>
    public void Set(Account? account)
    {
        if (Value == account)
        {
            return;
        }

        Value = account;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Whether <paramref name="person"/> is the signed-in account. A session restored while offline
    /// carries a name but no identifier, and an unknown identifier owns nothing — better to hide the
    /// edit button from its owner than to offer it on somebody else's comment.
    /// </summary>
    public bool Owns(PersonId person) =>
        person.IsValid && Value is { Id.IsValid: true } account && account.Id == person;
}
