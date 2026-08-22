namespace Lemmy.Services;

/// <summary>Loads and saves <see cref="AppSettings"/>.</summary>
public interface IAppSettingsStore
{
    /// <summary>
    /// Reads the stored settings, falling back to <see cref="AppSettings.Default"/> when nothing is
    /// stored or what is stored cannot be read. Never throws: a corrupt settings file should cost
    /// the reader their preferences, not the ability to start the app.
    /// </summary>
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the settings.</summary>
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
