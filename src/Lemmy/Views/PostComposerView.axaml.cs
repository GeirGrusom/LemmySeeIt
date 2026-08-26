using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lemmy.Views;

/// <summary>The form for writing or rewriting a post.</summary>
public partial class PostComposerView : UserControl
{
    /// <summary>Creates the view.</summary>
    public PostComposerView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
