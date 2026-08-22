using Avalonia.Controls;
using Lemmy.ViewModels;

namespace Lemmy.Views;

/// <summary>
/// Shared plumbing for the pull-to-refresh gesture. Every list surface handles it the same way, and
/// the only part worth getting right is the deferral: holding it until the fetch finishes is what
/// keeps the spinner on screen for the duration instead of snapping back the moment you let go.
/// </summary>
internal static class RefreshGesture
{
    /// <summary>Starts a refresh for the page bound to the pulled view.</summary>
    internal static void Begin(object? dataContext, RefreshRequestedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (dataContext is not PageViewModel page)
        {
            return;
        }

        // Taken before the await so the visualizer is already held when the fetch starts.
        RefreshCompletionDeferral deferral = args.GetDeferral();
        _ = RunAsync(page, deferral);
    }

    private static async Task RunAsync(PageViewModel page, RefreshCompletionDeferral deferral)
    {
        try
        {
            // The page turns its own failures into a message on itself, so nothing to catch here.
            await page.RefreshAsync().ConfigureAwait(true);
        }
        finally
        {
            deferral.Complete();
        }
    }
}
