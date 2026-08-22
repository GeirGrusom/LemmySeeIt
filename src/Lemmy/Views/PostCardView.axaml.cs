using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lemmy.Views;

/// <summary>One row in a feed.</summary>
public sealed partial class PostCardView : UserControl
{
    /// <summary>Creates the row.</summary>
    public PostCardView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
