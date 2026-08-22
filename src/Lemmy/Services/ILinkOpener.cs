using Lemmy.Domain;

namespace Lemmy.Services;

/// <summary>Hands a link to whatever the platform uses to open one — normally the web browser.</summary>
public interface ILinkOpener
{
    /// <summary>
    /// Opens <paramref name="link"/> outside the app, reporting whether the platform accepted it.
    /// Never throws: a link that will not open is a disappointment, not an error worth interrupting
    /// the reader over.
    /// </summary>
    Task<bool> OpenAsync(WebLink link);
}
