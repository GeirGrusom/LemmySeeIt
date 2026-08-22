using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lemmy.Views;

/// <summary>A single post and its comment thread.</summary>
public sealed partial class PostDetailView : UserControl
{
    /// <summary>Creates the post view.</summary>
    public PostDetailView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
