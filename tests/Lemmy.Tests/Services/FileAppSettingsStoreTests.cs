using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.Tests.Services;

[TestFixture]
internal sealed class FileAppSettingsStoreTests
{
    private string directory = string.Empty;
    private string filePath = string.Empty;

    [SetUp]
    public void CreateTemporaryDirectory()
    {
        directory = Path.Combine(Path.GetTempPath(), $"lemmyseeit-tests-{Guid.NewGuid():N}");
        filePath = Path.Combine(directory, "nested", "settings.json");
    }

    [TearDown]
    public void RemoveTemporaryDirectory()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task LoadAsync_ReturnsTheDefaultsWhenNothingHasBeenSaved()
    {
        var store = new FileAppSettingsStore(filePath);

        Assert.That(await store.LoadAsync(), Is.EqualTo(AppSettings.Default));
    }

    [Test]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsEveryField()
    {
        var store = new FileAppSettingsStore(filePath);
        var settings = new AppSettings(
            InstanceAddress.Parse("sh.itjust.works"),
            ListingType.Local,
            PostSortType.TopWeek,
            CommentSortType.New,
            ShowNsfw: true,
            BlurNsfwImages: false);

        await store.SaveAsync(settings);

        Assert.That(await store.LoadAsync(), Is.EqualTo(settings));
    }

    [Test]
    public async Task SaveAsync_CreatesTheDirectoryItNeeds()
    {
        var store = new FileAppSettingsStore(filePath);

        await store.SaveAsync(AppSettings.Default);

        Assert.That(File.Exists(filePath), Is.True);
    }

    /// <summary>The write goes via a temporary file; none of those should be left behind.</summary>
    [Test]
    public async Task SaveAsync_LeavesNoTemporaryFileBehind()
    {
        var store = new FileAppSettingsStore(filePath);

        await store.SaveAsync(AppSettings.Default);

        Assert.That(Directory.EnumerateFiles(Path.GetDirectoryName(filePath)!, "*.tmp"), Is.Empty);
    }

    /// <summary>Losing preferences is annoying; failing to launch over them is not acceptable.</summary>
    [Test]
    public async Task LoadAsync_FallsBackToTheDefaultsWhenTheFileIsCorrupt()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, "{ this is not json");

        Assert.That(await new FileAppSettingsStore(filePath).LoadAsync(), Is.EqualTo(AppSettings.Default));
    }

    [Test]
    public async Task LoadAsync_FallsBackFieldByFieldWhenAStoredValueIsNoLongerValid()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(
            filePath,
            """{"instance":"not a host","listing":"NoSuchListing","sort":"TopWeek","showNsfw":true}""");

        AppSettings settings = await new FileAppSettingsStore(filePath).LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(settings.Instance, Is.EqualTo(AppSettings.Default.Instance), "an unusable host falls back");
            Assert.That(settings.Listing, Is.EqualTo(AppSettings.Default.Listing), "an unknown enum name falls back");
            Assert.That(settings.Sort, Is.EqualTo(PostSortType.TopWeek), "a valid field is still honoured");
            Assert.That(settings.ShowNsfw, Is.True);
        });
    }

    [Test]
    public async Task SaveAsync_OverwritesAPreviousSave()
    {
        var store = new FileAppSettingsStore(filePath);
        await store.SaveAsync(AppSettings.Default);

        AppSettings changed = AppSettings.Default with { Sort = PostSortType.New };
        await store.SaveAsync(changed);

        Assert.That(await store.LoadAsync(), Is.EqualTo(changed));
    }

    [Test]
    public void Constructor_RejectsABlankPath() =>
        Assert.That(() => new FileAppSettingsStore("  "), Throws.TypeOf<ArgumentException>());
}
