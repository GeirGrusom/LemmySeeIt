using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lemmy.Views;

/// <summary>An account's page.</summary>
public partial class ProfileView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ProfileView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
