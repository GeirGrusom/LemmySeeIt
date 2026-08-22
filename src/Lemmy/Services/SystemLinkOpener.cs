using Avalonia.Controls;
using Lemmy.Domain;

namespace Lemmy.Services;

/// <summary>
/// Opens links through the platform's own launcher: the browser on desktop, whatever the user has
/// chosen for the scheme on Android and iOS.
/// </summary>
/// <remarks>
/// The launcher hangs off the window, which does not exist when services are built and differs
/// between a desktop window and a mobile activity's view. Rather than guess at it from the
/// application lifetime — which varies by platform — the host is handed in once the shell is on
/// screen, and taken away again when it is not.
/// </remarks>
public sealed class SystemLinkOpener : ILinkOpener
{
    private TopLevel? host;

    /// <summary>Points the opener at the window currently on screen, or at nothing.</summary>
    public void Attach(TopLevel? topLevel) => host = topLevel;

    /// <inheritdoc />
    public async Task<bool> OpenAsync(WebLink link)
    {
        if (!link.IsValid || host is null)
        {
            return false;
        }

        try
        {
            return await host.Launcher.LaunchUriAsync(link.ToUri()).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or PlatformNotSupportedException or UriFormatException)
        {
            return false;
        }
    }
}
