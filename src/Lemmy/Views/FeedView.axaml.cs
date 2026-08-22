using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lemmy.ViewModels;

namespace Lemmy.Views;

/// <summary>
/// A feed. The one thing that cannot be expressed in markup is "fetch the next page when the reader
/// nears the bottom", so the scroll position is watched here and the view model decides what to do
/// about it.
/// </summary>
public sealed partial class FeedView : UserControl
{
    /// <summary>How close to the end, in pixels, counts as "nearly there".</summary>
    private const double LoadMoreThreshold = 900;

    private readonly ScrollMemory scrollMemory;

    /// <summary>Creates the feed view.</summary>
    public FeedView()
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
        // The handler is attached to this control, so the scroller is the event's source, not its sender.
        if (DataContext is not FeedViewModel feed || e.Source is not ScrollViewer scrollViewer)
        {
            return;
        }

        scrollMemory.Remember();

        double remaining = scrollViewer.Extent.Height - scrollViewer.Offset.Y - scrollViewer.Viewport.Height;
        if (remaining <= LoadMoreThreshold)
        {
            // The view model refuses overlapping and out-of-range requests, so this can fire freely.
            _ = feed.LoadMoreAsync();
        }
    }
}
