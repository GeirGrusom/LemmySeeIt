using Lemmy.Domain;

namespace Lemmy.ViewModels;

/// <summary>One server offered in the picker.</summary>
/// <param name="Address">The server.</param>
/// <param name="IsCurrent">Whether the app is already pointed at it.</param>
/// <param name="IsRemembered">
/// Whether the reader has used it before, as opposed to it being one of the suggestions the app
/// shipped with.
/// </param>
public readonly record struct InstanceChoice(InstanceAddress Address, bool IsCurrent, bool IsRemembered)
{
    /// <summary>The server address, as shown.</summary>
    public string Label => Address.Value;

    /// <summary>Where this one came from, for the chip beside it.</summary>
    public string OriginLabel => IsRemembered ? "used before" : "suggested";
}
