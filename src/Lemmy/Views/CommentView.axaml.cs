using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lemmy.Views;

/// <summary>One comment and, through itself, its replies.</summary>
public sealed partial class CommentView : UserControl
{
    /// <summary>Creates the comment view.</summary>
    public CommentView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
