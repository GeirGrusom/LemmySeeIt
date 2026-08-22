using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lemmy.Views;

/// <summary>The subscribe and unsubscribe control for a community.</summary>
public partial class SubscribeButtonView : UserControl
{
    /// <summary>Creates the view.</summary>
    public SubscribeButtonView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
