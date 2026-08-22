using System.Text.Json;
using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Services;

/// <summary>
/// Stores settings as JSON in the platform's per-user application data folder, which resolves to
/// app-private storage on Android and iOS and to the roaming profile on Windows.
/// </summary>
public sealed class FileAppSettingsStore : IAppSettingsStore
{
    private const string FolderName = "LemmySeeIt";
    private const string FileName = "settings.json";

    private readonly string filePath;

    /// <summary>Uses the platform's default application data location.</summary>
    public FileAppSettingsStore()
        : this(DefaultPath())
    {
    }

    /// <summary>Uses an explicit path, which is what the tests do.</summary>
    public FileAppSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
    }

    /// <inheritdoc />
    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        SettingsDocument? document;
        try
        {
            if (!File.Exists(filePath))
            {
                return AppSettings.Default;
            }

            await using FileStream stream = File.OpenRead(filePath);
            document = await JsonSerializer
                .DeserializeAsync(stream, SettingsJsonContext.Default.SettingsDocument, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Unreadable settings are not worth failing a launch over.
            return AppSettings.Default;
        }

        return document is null ? AppSettings.Default : FromDocument(document);
    }

    /// <inheritdoc />
    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write beside the target and move into place, so a crash mid-write cannot leave a
        // half-written settings file that the next launch would silently discard.
        string temporaryPath = filePath + ".tmp";

        await using (FileStream stream = File.Create(temporaryPath))
        {
            await JsonSerializer
                .SerializeAsync(stream, ToDocument(settings), SettingsJsonContext.Default.SettingsDocument, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(temporaryPath, filePath, overwrite: true);
    }

    private static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
            FolderName,
            FileName);

    private static SettingsDocument ToDocument(AppSettings settings) => new()
    {
        Instance = settings.Instance.Value,
        Listing = settings.Listing.ToString(),
        Sort = settings.Sort.ToString(),
        CommentSort = settings.CommentSort.ToString(),
        ShowNsfw = settings.ShowNsfw,
        BlurNsfwImages = settings.BlurNsfwImages,
    };

    private static AppSettings FromDocument(SettingsDocument document)
    {
        InstanceAddress instance = InstanceAddress.TryParse(document.Instance.AsSpan(), out InstanceAddress parsed)
            ? parsed
            : AppSettings.Default.Instance;

        return new AppSettings(
            instance,
            ParseEnum(document.Listing, AppSettings.Default.Listing),
            ParseEnum(document.Sort, AppSettings.Default.Sort),
            ParseEnum(document.CommentSort, AppSettings.Default.CommentSort),
            document.ShowNsfw,
            document.BlurNsfwImages);
    }

    /// <summary>
    /// Reads an enum by name, falling back rather than throwing. <see cref="Enum.TryParse{TEnum}(string?, out TEnum)"/>
    /// is trim-safe for a concrete type argument, so this stays Native AOT compatible.
    /// </summary>
    private static TEnum ParseEnum<TEnum>(string? text, TEnum fallback)
        where TEnum : struct, Enum =>
        Enum.TryParse(text, ignoreCase: true, out TEnum value) && Enum.IsDefined(value) ? value : fallback;
}
