using Avalonia.Controls;
using Avalonia.Input;

namespace Lemmy.Views;

/// <summary>
/// Text that can be selected with a mouse but not with a finger.
/// </summary>
/// <remarks>
/// Avalonia reports a touch contact as a pressed left button, so <see cref="SelectableTextBlock"/>
/// treats the start of a scroll as the start of a selection: dragging a thread would leave a line
/// highlighted behind it. Worse, none of what makes selection useful exists on a phone — there are
/// no drag handles to adjust it and no long-press menu to copy it — so what the reader got was the
/// cost without the feature.
///
/// The check is on the pointer rather than the platform, so a touchscreen laptop still selects with
/// its mouse and scrolls with a finger. The events are left unhandled either way, which is what
/// lets the surrounding scroll gesture carry on as though this control were not here.
/// </remarks>
public class SelectableText : SelectableTextBlock
{
    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }

        base.OnPointerPressed(e);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }

        base.OnPointerMoved(e);
    }
}
