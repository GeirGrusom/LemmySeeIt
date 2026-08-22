using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lemmy.Views;

/// <summary>The upvote and downvote arrows with the running score between them.</summary>
public partial class VoteBarView : UserControl
{
    /// <summary>Creates the view.</summary>
    public VoteBarView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
