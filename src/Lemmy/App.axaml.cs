using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lemmy.Services;
using Lemmy.ViewModels;
using Lemmy.Views;

namespace Lemmy;

/// <summary>
/// The application. Builds the one <see cref="MainViewModel"/> the app has and hands it to whichever
/// lifetime the platform provides: a window on Windows and Linux, a single view on Android and iOS.
/// </summary>
public sealed partial class App : Application
{
    /// <summary>Sent on every outbound request so instances can identify (and rate-limit) this client.</summary>
    private const string UserAgent = "LemmySeeIt/0.1 (+https://github.com/lemmyseeit)";

    /// <summary>
    /// Set by a platform head before the app starts, when that platform has a better place to keep
    /// a session than anything the shared project can reach. Android does; the desktops do not.
    /// </summary>
    public static ISessionStore? SessionStoreOverride { get; set; }

    private AppServices? services;
    private MainViewModel? mainViewModel;

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        services = AppServices.CreateDefault(UserAgent, SessionStoreOverride);
        mainViewModel = new MainViewModel(services);

        // The link opener needs whatever window is on screen, and where that comes from differs by
        // platform, so each lifetime hands its own over rather than the opener guessing.
        var linkOpener = services.LinkOpener as SystemLinkOpener;

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
#if DEBUG
                // Attached here rather than in Initialize: the inspector is a desktop affordance,
                // and Initialize also runs under the headless test host, where a second attachment
                // throws.
                this.AttachDeveloperTools();
#endif
                var window = new MainWindow { DataContext = mainViewModel };
                linkOpener?.Attach(window);
                desktop.MainWindow = window;
                desktop.ShutdownRequested += (_, _) => Teardown();
                break;

            case IActivityApplicationLifetime activity:
                activity.MainViewFactory = () => CreateMainView(mainViewModel, linkOpener);
                break;

            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = CreateMainView(mainViewModel, linkOpener);
                break;
        }

        base.OnFrameworkInitializationCompleted();

        // Deliberately not awaited: the first frame should not wait on the network or on disk.
        _ = mainViewModel.InitialiseAsync();
    }

    /// <summary>
    /// Builds the shell for the single-view platforms. Unlike a window, a view has no launcher of
    /// its own until it is on screen, so the opener is pointed at its window when it gets one.
    /// </summary>
    private static MainView CreateMainView(MainViewModel viewModel, SystemLinkOpener? linkOpener)
    {
        var view = new MainView { DataContext = viewModel };

        view.AttachedToVisualTree += (_, _) => linkOpener?.Attach(TopLevel.GetTopLevel(view));
        view.DetachedFromVisualTree += (_, _) => linkOpener?.Attach(null);

        return view;
    }

    private void Teardown()
    {
        mainViewModel?.Dispose();

        if (services?.ImageLoader is IDisposable imageLoader)
        {
            imageLoader.Dispose();
        }

        if (services?.ApiFactory is IDisposable apiFactory)
        {
            apiFactory.Dispose();
        }
    }
}
