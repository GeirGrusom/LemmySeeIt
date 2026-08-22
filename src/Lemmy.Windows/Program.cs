using Avalonia;
using Lemmy.Services;

namespace Lemmy.Windows;

/// <summary>
/// The Windows entry point. Kept separate from the Linux head so each can publish for its own
/// runtime identifier and carry only what its platform needs — here, an application manifest that
/// declares per-monitor DPI awareness.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Starts the app. Nothing that touches Avalonia, a third-party library or the synchronization
    /// context may run before <see cref="BuildAvaloniaApp"/>: none of it is initialised yet.
    /// </summary>
    [STAThread]
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
