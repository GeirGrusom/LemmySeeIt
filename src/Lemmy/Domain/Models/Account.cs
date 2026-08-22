namespace Lemmy.Domain.Models;

/// <summary>The signed-in account, as the instance reports it.</summary>
/// <param name="Id">Who this is on that instance.</param>
/// <param name="Name">The account name.</param>
/// <param name="DisplayName">The chosen display name, when there is one.</param>
/// <param name="Instance">Where the account lives. Sessions do not travel between instances.</param>
/// <param name="Avatar">The profile picture, when there is one.</param>
/// <param name="ShowNsfw">Whether the account is set to see content flagged not safe for work.</param>
/// <param name="BlurNsfw">Whether the account is set to blur such images until tapped.</param>
public sealed record Account(
    PersonId Id,
    Username Name,
    string? DisplayName,
    InstanceAddress Instance,
    WebLink? Avatar,
    bool ShowNsfw = false,
    bool BlurNsfw = true)
{
    /// <summary>What to show in the account row.</summary>
    public string PreferredName => string.IsNullOrWhiteSpace(DisplayName) ? Name.Value : DisplayName;

    /// <summary>The unambiguous <c>@name@instance</c> form.</summary>
    public string QualifiedName => $"@{Name.Value}@{Instance.Value}";
}
