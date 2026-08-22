using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace Lemmy.Android;

/// <summary>
/// The launcher activity. The configuration changes listed here are handled by Avalonia's own
/// layout pass, so declaring them stops Android from tearing the activity down for a rotation or a
/// switch between light and dark.
/// </summary>
[Activity(
    Label = "LemmySeeIt",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/Icon",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.UiMode
        | ConfigChanges.Density)]
public sealed class MainActivity : AvaloniaMainActivity
{
}
