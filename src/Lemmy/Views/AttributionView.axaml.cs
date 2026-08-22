using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lemmy.Views;

/// <summary>The licences and attribution page.</summary>
public partial class AttributionView : UserControl
{
    /// <summary>Creates the view.</summary>
    public AttributionView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
