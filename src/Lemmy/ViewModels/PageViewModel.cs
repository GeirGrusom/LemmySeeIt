using CommunityToolkit.Mvvm.ComponentModel;
using Lemmy.Api;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>
/// A whole screen. Owns the three states every screen in this app can be in — loading, failed,
/// showing content — and a cancellation token that is tripped when the screen goes away, so a slow
/// response cannot land on a page the reader has already left.
/// </summary>
public abstract partial class PageViewModel : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource lifetime = new();

    private bool isDisposed;

    /// <summary>Creates a page over the given services and navigator.</summary>
    protected PageViewModel(AppServices services, INavigator navigator)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(navigator);

        Services = services;
        Navigator = navigator;
    }

    /// <summary>The page's heading.</summary>
    public abstract string Title { get; }

    /// <summary>Whether a request is in flight for the first screenful.</summary>
    [ObservableProperty]
    private bool isBusy;

    /// <summary>What went wrong, or <see langword="null"/> when nothing has.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    /// <summary>Whether the page is showing an error instead of content.</summary>
    public bool HasError => ErrorMessage is not null;

    partial void OnIsBusyChanged(bool value) => OnBusyChanged(value);

    /// <summary>Called when <see cref="IsBusy"/> flips, for pages with derived state to announce.</summary>
    protected virtual void OnBusyChanged(bool isBusy)
    {
    }

    /// <summary>The services this page was given.</summary>
    protected AppServices Services { get; }

    /// <summary>Where this page sends the reader next.</summary>
    protected INavigator Navigator { get; }

    /// <summary>
    /// Which row the page's list was showing when the reader last navigated away. View state rather
    /// than page state, but it belongs to the page's lifetime: it has to outlive the control, and it
    /// has to die with the page rather than leak into the next one.
    /// </summary>
    internal ScrollAnchor ScrollAnchor { get; set; }

    /// <summary>
    /// Cancelled when the page is disposed. Reads as already-cancelled afterwards rather than
    /// throwing: a scroll handler can still fire once after the reader has navigated away.
    /// </summary>
    protected CancellationToken Lifetime => isDisposed ? new CancellationToken(canceled: true) : lifetime.Token;

    /// <summary>Whether the page has been torn down.</summary>
    protected bool IsDisposed => isDisposed;

    /// <summary>Fetches whatever the page shows. Called once, when the page is first displayed.</summary>
    public abstract Task LoadAsync();

    /// <summary>
    /// Re-fetches from the top, discarding what is loaded. This is what the pull-to-refresh gesture
    /// calls; for most pages that is the same work as the first load, so the default defers to it.
    /// </summary>
    public virtual Task RefreshAsync() => LoadAsync();

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Cancels in-flight work and releases anything the page owns.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || isDisposed)
        {
            return;
        }

        isDisposed = true;

        if (!lifetime.IsCancellationRequested)
        {
            lifetime.Cancel();
        }

        lifetime.Dispose();
    }

    /// <summary>
    /// Runs <paramref name="work"/> with the busy flag set, turning the failures a Lemmy server can
    /// hand us into a message on the page. Cancellation is silent: the reader caused it.
    /// </summary>
    protected async Task RunAsync(Func<CancellationToken, Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (isDisposed)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await work(Lifetime).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The page went away, or the reader asked for something else.
        }
        catch (LemmyApiException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
