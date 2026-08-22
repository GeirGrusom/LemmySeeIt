using Avalonia;
using Avalonia.Headless;
using Lemmy;
using Lemmy.Tests.Ui;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Lemmy.Tests.Ui;

/// <summary>
/// Starts the real <see cref="App"/> on Avalonia's headless platform, with Skia doing the actual
/// drawing. Running the genuine application — its styles, its theme dictionaries, its view locator
/// — is the point: a compiled binding that names a property nothing has still fails at run time,
/// and that failure should surface here rather than on a device.
/// </summary>
internal static class TestAppBuilder
{
    /// <summary>Called by the headless NUnit integration to build the application under test.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
