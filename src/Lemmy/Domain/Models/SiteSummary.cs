using Lemmy.Domain;

namespace Lemmy.Domain.Models;

/// <summary>The identity of an instance, as shown when picking or confirming a server.</summary>
/// <param name="Address">Where the instance lives.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Description">Its short tagline.</param>
/// <param name="Sidebar">Its long-form sidebar text.</param>
/// <param name="Icon">Its icon, when there is one.</param>
/// <param name="Banner">Its banner, when there is one.</param>
/// <param name="SoftwareVersion">The Lemmy version it runs, which decides what the API supports.</param>
/// <param name="Users">Registered accounts.</param>
/// <param name="Communities">Communities hosted locally.</param>
/// <param name="Registration">Whether it is taking new accounts, and on what terms.</param>
/// <param name="ApplicationQuestion">What it asks applicants, when it asks anything.</param>
/// <param name="RequiresEmailVerification">Whether a new account must confirm an email address.</param>
public sealed record SiteSummary(
    InstanceAddress Address,
    string Name,
    string? Description,
    MarkdownText Sidebar,
    WebLink? Icon,
    WebLink? Banner,
    string? SoftwareVersion,
    VoteCount Users,
    VoteCount Communities,
    RegistrationMode Registration = RegistrationMode.Unknown,
    MarkdownText ApplicationQuestion = default,
    bool RequiresEmailVerification = false);
