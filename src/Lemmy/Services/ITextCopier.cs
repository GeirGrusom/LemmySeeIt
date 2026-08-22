namespace Lemmy.Services;

/// <summary>
/// Puts text on the system clipboard.
/// </summary>
/// <remarks>
/// Separate from selecting text, and on a phone the only way to copy any: a finger cannot select,
/// so there has to be something to press instead. Abstracted for the same reason as
/// <see cref="ILinkOpener"/> — the clipboard hangs off the window, which the view models do not
/// have and should not need.
/// </remarks>
public interface ITextCopier
{
    /// <summary>
    /// Copies <paramref name="text"/>, answering whether it went. Blank text is not copied: it
    /// would silently replace whatever the reader already had on the clipboard with nothing.
    /// </summary>
    Task<bool> CopyAsync(string? text);
}
