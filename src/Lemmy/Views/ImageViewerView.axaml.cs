using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Lemmy.ViewModels;

namespace Lemmy.Views;

/// <summary>
/// The full-screen image. Owns the gestures — pinch, drag, double-tap, wheel — and hands each one
/// to <see cref="ZoomState"/> to work out what it means; the arithmetic lives there so it can be
/// tested without a touchscreen.
/// </summary>
public sealed partial class ImageViewerView : UserControl
{
    /// <summary>How much one wheel notch zooms, for the desktop heads.</summary>
    private const double WheelZoomStep = 1.15;

    /// <summary>
    /// How long a tap waits before it counts as "dismiss". Both gestures start with the same tap, so
    /// closing immediately would fire on the first half of every double-tap — which is exactly what
    /// it did: the viewer closed and the second tap landed on the feed behind it.
    /// </summary>
    private static readonly TimeSpan DoubleTapWindow = TimeSpan.FromMilliseconds(260);

    /// <summary>How far a flick has to travel before it means "next picture" rather than "dismiss".</summary>
    private const double SwipeThreshold = 60;

    /// <summary>
    /// How often to check which animation frame is due. Faster than any sane GIF, so frame timings
    /// are honoured rather than rounded up to whatever tick rate happened to be chosen.
    /// </summary>
    private static readonly TimeSpan FrameTick = TimeSpan.FromMilliseconds(20);

    private readonly Panel? surface;
    private readonly Image? picture;
    private readonly DispatcherTimer closeTimer;
    private readonly DispatcherTimer animationTimer;
    private readonly Stopwatch animationClock = new();

    private Point? dragOrigin;
    private Vector totalDrag;
    private bool didDrag;

    /// <summary>Creates the viewer.</summary>
    public ImageViewerView()
    {
        InitializeComponent();

        surface = this.FindControl<Panel>("Surface");
        picture = this.FindControl<Image>("Picture");

        closeTimer = new DispatcherTimer { Interval = DoubleTapWindow };
        closeTimer.Tick += OnCloseTimerTick;

        animationTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = FrameTick };
        animationTimer.Tick += OnAnimationTick;

        // Pinch only reaches the control if something is watching for it.
        GestureRecognizers.Add(new PinchGestureRecognizer());

        AddHandler(PinchEvent, OnPinch);
        AddHandler(PinchEndedEvent, OnPinchEnded);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelPendingClose();
        StopAnimating();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        if (ViewModel is { } previous)
        {
            previous.PropertyChanged -= OnViewModelPropertyChanged;
        }

        base.OnDataContextChanged(e);

        if (ViewModel is { } current)
        {
            current.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyTransform();
        SyncAnimation();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A picture only reveals whether it animates once it has finished downloading, and the
        // reader can flick from an animation to a still without the view being rebuilt.
        if (e.PropertyName is nameof(ImageViewerViewModel.IsAnimated))
        {
            SyncAnimation();
        }
    }

    /// <summary>Starts or stops the frame clock to match what is on screen.</summary>
    private void SyncAnimation()
    {
        if (ViewModel is { IsAnimated: true })
        {
            animationClock.Restart();
            animationTimer.Start();
            return;
        }

        StopAnimating();
    }

    private void StopAnimating()
    {
        animationTimer.Stop();
        animationClock.Reset();
    }

    private void OnAnimationTick(object? sender, EventArgs e) => ViewModel?.ShowFrameAt(animationClock.Elapsed);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty)
        {
            // The window resized or rotated; what was a legal pan may no longer be one.
            Clamp();
        }
    }

    /// <inheritdoc />
    protected override void OnDataContextEndUpdate()
    {
        base.OnDataContextEndUpdate();
        SyncAnimation();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private ImageViewerViewModel? ViewModel => DataContext as ImageViewerViewModel;

    /// <summary>
    /// The size the image occupies before any zoom — the box a <c>Uniform</c> stretch fits it into.
    /// Every gesture is measured against this rather than against the bitmap's pixel size.
    /// </summary>
    private Size FittedSize()
    {
        if (ViewModel?.Image is not { } bitmap || surface is null)
        {
            return default;
        }

        Size viewport = surface.Bounds.Size;
        Size natural = bitmap.Size;

        if (viewport.Width <= 0 || viewport.Height <= 0 || natural.Width <= 0 || natural.Height <= 0)
        {
            return default;
        }

        double scale = Math.Min(viewport.Width / natural.Width, viewport.Height / natural.Height);
        return new Size(natural.Width * scale, natural.Height * scale);
    }

    private Size Viewport => surface?.Bounds.Size ?? default;

    private void Update(ZoomState state)
    {
        ViewModel?.Apply(state);
        ApplyTransform();
    }

    private void Clamp()
    {
        if (ViewModel is { } model)
        {
            Update(model.Zoom.Clamped(Viewport, FittedSize()));
        }
    }

    private void ApplyTransform()
    {
        if (picture is null || ViewModel is not { } model)
        {
            return;
        }

        ZoomState zoom = model.Zoom;

        picture.RenderTransform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(zoom.Scale, zoom.Scale),
                new TranslateTransform(zoom.Offset.X, zoom.Offset.Y),
            },
        };
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (ViewModel is not { } model)
        {
            return;
        }

        // ScaleOrigin is a fraction of the control; the geometry works in pixels.
        var origin = new Point(e.ScaleOrigin.X * Viewport.Width, e.ScaleOrigin.Y * Viewport.Height);

        Update(model.Zoom.ScaledBy(e.Scale, origin, Viewport, FittedSize()));
        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e) => Clamp();

    /// <inheritdoc />
    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);

        // The tap that started this was going to dismiss the viewer; it meant zoom instead.
        CancelPendingClose();

        if (ViewModel is not { } model)
        {
            return;
        }

        Update(model.Zoom.ToggledAt(e.GetPosition(this), Viewport, FittedSize()));
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (ViewModel is not { } model)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Left when model.CanShowPrevious:
                _ = model.ShowPreviousAsync();
                break;
            case Key.Right when model.CanShowNext:
                _ = model.ShowNextAsync();
                break;
            case Key.Escape:
                model.Close();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (ViewModel is not { } model)
        {
            return;
        }

        double factor = e.Delta.Y > 0 ? WheelZoomStep : 1 / WheelZoomStep;
        Update(model.Zoom.ScaledBy(factor, e.GetPosition(this), Viewport, FittedSize()));
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        CancelPendingClose();

        dragOrigin = e.GetPosition(this);
        totalDrag = default;
        didDrag = false;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (dragOrigin is not { } origin || ViewModel is not { } model)
        {
            return;
        }

        Point current = e.GetPosition(this);
        var delta = new Vector(current.X - origin.X, current.Y - origin.Y);

        if (Math.Abs(delta.X) + Math.Abs(delta.Y) < 2)
        {
            return;
        }

        dragOrigin = current;
        totalDrag += delta;
        didDrag = true;

        // Zoomed in, a drag moves the picture. Zoomed out there is nothing to move, so the same
        // drag is a flick between pictures instead — resolved on release, once its direction is known.
        if (model.Zoom.IsZoomed)
        {
            Update(model.Zoom.PannedBy(delta, Viewport, FittedSize()));
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        bool wasDrag = didDrag;
        Vector travelled = totalDrag;

        dragOrigin = null;
        totalDrag = default;
        didDrag = false;

        if (ViewModel is not { } model)
        {
            return;
        }

        if (!model.Zoom.IsZoomed && wasDrag && TrySwipe(travelled, model))
        {
            return;
        }

        // A tap on the picture closes the viewer, but only when it was a tap: dragging a zoomed
        // image around must not dismiss it, and neither should letting go after a pinch. The close
        // waits out the double-tap window in case a second tap is coming.
        if (!wasDrag && !model.IsLoading && !model.Zoom.IsZoomed)
        {
            closeTimer.Start();
        }
    }

    /// <summary>
    /// Turns a horizontal flick into a step through the gallery. Mostly-vertical drags are left
    /// alone: they are how a reader scrolls a tall comic page once zoomed, and misreading one as a
    /// page turn would be maddening.
    /// </summary>
    private static bool TrySwipe(Vector travelled, ImageViewerViewModel model)
    {
        if (Math.Abs(travelled.X) < SwipeThreshold || Math.Abs(travelled.X) <= Math.Abs(travelled.Y))
        {
            return false;
        }

        if (travelled.X < 0 && model.CanShowNext)
        {
            _ = model.ShowNextAsync();
            return true;
        }

        if (travelled.X > 0 && model.CanShowPrevious)
        {
            _ = model.ShowPreviousAsync();
            return true;
        }

        // At either end of the gallery the flick has nowhere to go; swallow it rather than letting
        // it fall through and dismiss the viewer.
        return true;
    }

    private void CancelPendingClose() => closeTimer.Stop();

    private void OnCloseTimerTick(object? sender, EventArgs e)
    {
        closeTimer.Stop();
        ViewModel?.Close();
    }
}
