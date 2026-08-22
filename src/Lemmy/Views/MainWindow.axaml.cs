using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lemmy.Views;

/// <summary>The desktop window. Everything inside it is the same view the mobile heads show.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the window.</summary>
    public MainWindow() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
