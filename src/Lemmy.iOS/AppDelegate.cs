using Avalonia;
using Avalonia.iOS;
using Foundation;

namespace Lemmy.iOS;

/// <summary>
/// Launches the shared Avalonia app inside a UIKit application. iOS gives Avalonia a single-view
/// lifetime, which is the same one Android uses — hence one <c>MainView</c> for both.
/// </summary>
[Register("AppDelegate")]
#pragma warning disable CA1711 // "Delegate" suffix is required by UIKit, which looks this class up by name.
public sealed partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711
{
    /// <inheritdoc />
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).WithInterFont();
}
