using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lemmy.ViewModels;

namespace Lemmy.Views;

/// <summary>
/// The shell, shared by every platform. The desktop heads host it in a window; Android and iOS use
/// it directly as their single view, which is why nothing here assumes a title bar or a mouse.
/// </summary>
public sealed partial class MainView : UserControl
{
    private TopLevel? subscribedTopLevel;

    /// <summary>Creates the shell.</summary>
    public MainView() => InitializeComponent();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // The back gesture belongs to the window, not to this control, so the subscription has to
        // wait until there is a window to subscribe to.
        subscribedTopLevel = TopLevel.GetTopLevel(this);
        if (subscribedTopLevel is not null)
        {
            subscribedTopLevel.BackRequested += OnBackRequested;
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (subscribedTopLevel is not null)
        {
            subscribedTopLevel.BackRequested -= OnBackRequested;
            subscribedTopLevel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Answers Android's back button and gesture. Leaving the event unhandled is a deliberate
    /// outcome, not a failure: at the root of the app it is how the system gets to background us.
    /// </summary>
    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel shell && shell.TryGoBack())
        {
            e.Handled = true;
        }
    }
}
