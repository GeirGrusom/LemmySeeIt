namespace Lemmy.Services;

/// <summary>
/// The services every page needs, passed by hand rather than resolved from a container. With a
/// dependency graph this small, constructor arguments are clearer than registrations — and nothing
/// here depends on the reflection that a container would.
/// </summary>
/// <param name="ApiFactory">Creates read clients for an instance.</param>
/// <param name="ImageLoader">Fetches and decodes thumbnails.</param>
/// <param name="SettingsStore">Persists what the reader chose.</param>
/// <param name="LinkOpener">Hands links to the platform's browser.</param>
/// <param name="SessionStore">Where a signed-in session is kept between runs.</param>
/// <param name="TimeProvider">The clock, so "3h ago" is testable.</param>
/// <param name="Subscriptions">What the app knows about the account's subscriptions.</param>
public sealed record AppServices(
    ILemmyApiFactory ApiFactory,
    IImageLoader ImageLoader,
    IAppSettingsStore SettingsStore,
    ILinkOpener LinkOpener,
    ISessionStore SessionStore,
    TimeProvider TimeProvider,
    SubscriptionTracker Subscriptions)
{
    /// <summary>The real services, for an app that is actually running.</summary>
    public static AppServices CreateDefault(string userAgent, ISessionStore? sessionStore = null) =>
        new(
            new LemmyApiFactory(userAgent),
            new ImageLoader(userAgent),
            new FileAppSettingsStore(),
            new SystemLinkOpener(),
            sessionStore ?? SessionStores.CreateDefault(),
            TimeProvider.System,
            new SubscriptionTracker());

    /// <summary>The current time, as everything that formats a timestamp should ask for it.</summary>
    public DateTimeOffset Now => TimeProvider.GetUtcNow();
}
