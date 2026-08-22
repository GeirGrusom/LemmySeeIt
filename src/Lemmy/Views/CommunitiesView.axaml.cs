using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lemmy.ViewModels;

namespace Lemmy.Views;

/// <summary>The community directory, paged as the reader scrolls.</summary>
public sealed partial class CommunitiesView : UserControl
{
    /// <summary>How close to the end, in pixels, counts as "nearly there".</summary>
    private const double LoadMoreThreshold = 600;

    private readonly ScrollMemory scrollMemory;

    /// <summary>Creates the directory view.</summary>
    public CommunitiesView()
    {
        InitializeComponent();
        scrollMemory = new ScrollMemory(this);
        AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        scrollMemory.Attach();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        scrollMemory.Detach();
        base.OnDetachedFromVisualTree(e);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>Answers the pull-to-refresh gesture.</summary>
    private void OnRefreshRequested(object? sender, RefreshRequestedEventArgs e) =>
        RefreshGesture.Begin(DataContext, e);

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is not CommunitiesViewModel communities || e.Source is not ScrollViewer scrollViewer)
        {
            return;
        }

        scrollMemory.Remember();

        double remaining = scrollViewer.Extent.Height - scrollViewer.Offset.Y - scrollViewer.Viewport.Height;
        if (remaining <= LoadMoreThreshold)
        {
            _ = communities.LoadMoreAsync();
        }
    }
}
