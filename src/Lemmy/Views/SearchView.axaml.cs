using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lemmy.Views;

/// <summary>Search across one instance.</summary>
public sealed partial class SearchView : UserControl
{
    /// <summary>Creates the search view.</summary>
    public SearchView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
