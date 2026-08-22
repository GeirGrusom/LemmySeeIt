using Avalonia;
using Lemmy.Services;

namespace Lemmy.Linux;

/// <summary>
/// The Linux entry point. X11 and Wayland are both reached through <c>UsePlatformDetect</c>; the
/// separate head exists so this platform publishes against its own runtime identifier.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Starts the app. Nothing that touches Avalonia, a third-party library or the synchronization
    /// context may run before <see cref="BuildAvaloniaApp"/>: none of it is initialised yet.
    /// </summary>
    public static int Main(string[] args)
    {
        // This head ships a different set of packages from the shared project, so it hands
        // its own generated list to the licences page.
        Attribution.Use(Generated.GeneratedAttribution.Packages);

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Builds the app. Also called by the XAML designer, so it must stay parameterless.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
