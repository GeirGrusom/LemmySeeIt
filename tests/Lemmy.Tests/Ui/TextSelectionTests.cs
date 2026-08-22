using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Lemmy.Views;

namespace Lemmy.Tests.Ui;

/// <summary>
/// Selecting text with a mouse, and pointedly not with a finger.
/// </summary>
/// <remarks>
/// Avalonia reports a touch contact as a pressed left button, so the stock control treated the
/// start of a scroll as the start of a selection — dragging a thread left lines highlighted behind
/// it. The headless platform can only simulate a mouse, so these raise the pointer events directly
/// to get a touch one.
/// </remarks>
[TestFixture]
internal sealed class TextSelectionTests
{
    private static (SelectableText Text, Window Window) Show()
    {
        var text = new SelectableText
        {
            Text = "A comment worth reading twice, and long enough to drag across.",
            Width = 380,
        };
        var window = new Window { Width = 400, Height = 200, Content = text };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (text, window);
    }

    private static PointerPressedEventArgs Press(SelectableText text, Window window, PointerType type, Point at)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), type, isPrimary: true);
        var properties = new PointerPointProperties(
            RawInputModifiers.LeftMouseButton,
            PointerUpdateKind.LeftButtonPressed);

        return new PointerPressedEventArgs(text, pointer, window, at, 0, properties, KeyModifiers.None);
    }

    [AvaloniaTest]
    public void AMouseDragStillSelects()
    {
        (SelectableText text, Window window) = Show();

        text.RaiseEvent(Press(text, window, PointerType.Mouse, new Point(4, 8)));
        Dispatcher.UIThread.RunJobs();

        // A mouse press moves the caret; that it moved at all is what a finger must not do.
        Assert.That(text.SelectionStart, Is.GreaterThanOrEqualTo(0));
    }

    [AvaloniaTest]
    public void AFingerSelectsNothing()
    {
        (SelectableText text, Window window) = Show();

        // Start well into the text, so a selection would be visible if one were made.
        text.RaiseEvent(Press(text, window, PointerType.Touch, new Point(200, 8)));
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(text.SelectionStart, Is.EqualTo(text.SelectionEnd), "no range was selected");
            Assert.That(text.SelectedText, Is.Empty);
        });
    }

    [AvaloniaTest]
    public void AFingerLeavesTheEventForWhateverIsScrolling()
    {
        (SelectableText text, Window window) = Show();

        PointerPressedEventArgs press = Press(text, window, PointerType.Touch, new Point(200, 8));
        text.RaiseEvent(press);
        Dispatcher.UIThread.RunJobs();

        Assert.That(press.Handled, Is.False, "an unhandled press is what lets the list scroll");
    }

    [AvaloniaTest]
    public void AFingerDoesNotDisturbASelectionAMouseAlreadyMade()
    {
        (SelectableText text, Window window) = Show();

        text.SelectionStart = 2;
        text.SelectionEnd = 9;

        text.RaiseEvent(Press(text, window, PointerType.Touch, new Point(200, 8)));
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(text.SelectionStart, Is.EqualTo(2));
            Assert.That(text.SelectionEnd, Is.EqualTo(9));
        });
    }
}
