namespace Lemmy.Domain.Models;

/// <summary>An account, whether it lives on the instance being read or on a federated one.</summary>
/// <param name="Id">The instance-local identifier.</param>
/// <param name="Name">The account name, without instance suffix.</param>
/// <param name="DisplayName">The chosen display name, or <see langword="null"/> to fall back to <paramref name="Name"/>.</param>
/// <param name="ActorId">The fediverse-wide identity.</param>
/// <param name="Avatar">The profile picture, when there is one.</param>
/// <param name="IsLocal">Whether the account lives on the instance being read.</param>
/// <param name="IsBot">Whether the account is flagged as automated.</param>
/// <param name="IsBanned">Whether the account is banned instance-wide.</param>
/// <param name="IsDeleted">Whether the account has been deleted.</param>
/// <param name="Published">When the account was created.</param>
public sealed record Person(
    PersonId Id,
    Username Name,
    string? DisplayName,
    ActorId ActorId,
    WebLink? Avatar,
    bool IsLocal,
    bool IsBot,
    bool IsBanned,
    bool IsDeleted,
    DateTimeOffset Published)
{
    /// <summary>What to show in a byline: the display name when set, otherwise the account name.</summary>
    public string PreferredName => string.IsNullOrWhiteSpace(DisplayName) ? Name.Value : DisplayName;

    /// <summary>
    /// The unambiguous <c>@name@instance</c> form. Two accounts on different instances can share a
    /// name, so this is what belongs anywhere the reader might otherwise be misled.
    /// </summary>
    public string QualifiedName =>
        ActorId.Instance is { } instance ? $"@{Name.Value}@{instance.Value}" : $"@{Name.Value}";
}
