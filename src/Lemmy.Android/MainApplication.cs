using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Lemmy.Services;

namespace Lemmy.Android;

/// <summary>The Android application object, which builds the shared Avalonia app.</summary>
[Application(Label = "LemmySeeIt", Icon = "@drawable/Icon")]
public sealed class MainApplication : AvaloniaAndroidApplication<App>
{
    /// <summary>Called by the Android runtime when it constructs the application object.</summary>
    public MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    /// <inheritdoc />
    public override void OnCreate()
    {
        // Before base.OnCreate, which is where Avalonia builds the app and reads this. Setting it
        // afterwards is too late: the services are already built and quietly fall back to keeping
        // the session in memory, so a sign-in would not survive a restart.
        App.SessionStoreOverride = new KeystoreSessionStore(this);

        // This head carries AndroidX on top of the shared set.
        Attribution.Use(Generated.GeneratedAttribution.Packages);

        base.OnCreate();
    }

    /// <inheritdoc />
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).WithInterFont();
}
