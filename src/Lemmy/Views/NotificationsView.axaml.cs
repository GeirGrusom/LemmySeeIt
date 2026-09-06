using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lemmy.Views;

/// <summary>The replies and mentions addressed to the signed-in account.</summary>
public sealed partial class NotificationsView : UserControl
{
    /// <summary>Creates the notifications view.</summary>
    public NotificationsView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>Answers the pull-to-refresh gesture.</summary>
    private void OnRefreshRequested(object? sender, RefreshRequestedEventArgs e) =>
        RefreshGesture.Begin(DataContext, e);
}
