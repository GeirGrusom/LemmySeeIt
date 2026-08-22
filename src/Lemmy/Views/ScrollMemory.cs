using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Lemmy.ViewModels;

namespace Lemmy.Views;

/// <summary>
/// Remembers which row a scrolling page was showing and puts the reader back on it.
/// <para>
/// Restoring happens across layout passes rather than in one go, for two reasons. The row has to be
/// realised before its position can be measured, which takes a pass of its own; and the virtualising
/// panel keeps refining its extent estimate as more rows are built, nudging the offset each time.
/// So this scrolls to the row, measures how far off it landed, corrects, and repeats until the row
/// sits where it was — or until it has tried enough times to conclude it is not converging.
/// </para>
/// </summary>
internal sealed class ScrollMemory
{
    /// <summary>Close enough that no reader would notice.</summary>
    private const double ToleranceInPixels = 0.5;

    /// <summary>Enough passes to converge; a backstop against pinning the layout in a loop.</summary>
    private const int MaxCorrections = 12;

    private readonly Control owner;

    private ScrollViewer? scroller;
    private ListBox? list;
    private int corrections;
    private bool isRestored;

    internal ScrollMemory(Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        this.owner = owner;
    }

    /// <summary>Call from the view's <c>OnAttachedToVisualTree</c>.</summary>
    internal void Attach()
    {
        isRestored = false;
        corrections = 0;
        scroller = null;
        list = null;
        owner.LayoutUpdated += OnLayoutUpdated;
    }

    /// <summary>Call from the view's <c>OnDetachedFromVisualTree</c>, before the base call.</summary>
    internal void Detach()
    {
        owner.LayoutUpdated -= OnLayoutUpdated;
        Remember();
        scroller = null;
        list = null;
    }

    /// <summary>
    /// Records the row now at the top; called as the reader scrolls and again on the way out.
    /// Ignored while a restore is still pending, because the panel's own re-anchoring raises scroll
    /// events too and letting those overwrite the saved row would defeat the whole exercise.
    /// </summary>
    internal void Remember()
    {
        if (!isRestored || owner.DataContext is not PageViewModel page)
        {
            return;
        }

        if (Locate() is { } anchor)
        {
            page.ScrollAnchor = anchor;
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (isRestored || owner.DataContext is not PageViewModel page)
        {
            return;
        }

        ScrollAnchor anchor = page.ScrollAnchor;
        if (!anchor.IsMeaningful)
        {
            Finish();
            return;
        }

        if (FindList() is not { } items || FindScroller() is not { } view)
        {
            return;
        }

        if (anchor.Index >= items.ItemCount)
        {
            // The list came back shorter than it went away; the top is the honest answer.
            Finish();
            return;
        }

        if (++corrections > MaxCorrections)
        {
            Finish();
            return;
        }

        // Only ever move the offset directly. ScrollIntoView schedules its scroll rather than
        // applying it, so mixing the two makes each correction land on top of a jump that has not
        // happened yet, and the loop oscillates instead of converging.
        if (items.ContainerFromIndex(anchor.Index) is not { } container)
        {
            // The row is not built yet, so aim at where the panel's own average says it should be.
            // That realises rows nearer the target, and the next pass can measure properly.
            double average = view.Extent.Height / Math.Max(1, items.ItemCount);
            view.Offset = new Vector(view.Offset.X, Math.Max(0, (anchor.Index * average) - anchor.Delta));
            return;
        }

        double error = TopRelativeToViewport(container, view) - anchor.Delta;

        if (Math.Abs(error) <= ToleranceInPixels)
        {
            Finish();
            return;
        }

        view.Offset = new Vector(view.Offset.X, Math.Max(0, view.Offset.Y + error));
    }

    private void Finish()
    {
        isRestored = true;
        owner.LayoutUpdated -= OnLayoutUpdated;
    }

    /// <summary>The first row still visible at the top of the viewport, and how far it is cut off.</summary>
    private ScrollAnchor? Locate()
    {
        if (FindList() is not { } items || FindScroller() is not { } view)
        {
            return null;
        }

        int bestIndex = -1;
        double bestTop = 0;

        foreach (Control container in items.GetRealizedContainers())
        {
            double top = TopRelativeToViewport(container, view);

            // Skip rows that have scrolled entirely off the top.
            if (top + container.Bounds.Height <= 0)
            {
                continue;
            }

            int index = items.IndexFromContainer(container);
            if (index >= 0 && (bestIndex < 0 || index < bestIndex))
            {
                bestIndex = index;
                bestTop = top;
            }
        }

        return bestIndex < 0 ? null : new ScrollAnchor(bestIndex, Math.Min(0, bestTop));
    }

    private static double TopRelativeToViewport(Visual container, Visual view) =>
        container.TranslatePoint(default, view)?.Y ?? 0;

    private ScrollViewer? FindScroller() =>
        scroller ??= owner.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

    private ListBox? FindList() =>
        list ??= owner.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
}
