using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lemmy.Views;

/// <summary>The text box for writing a comment, a reply or an edit.</summary>
public partial class CommentComposerView : UserControl
{
    /// <summary>Creates the view.</summary>
    public CommentComposerView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
