using Lemmy.Domain;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

/// <summary>
/// The viewer opened on a picture from a body rather than a post: there is no page of pictures
/// around it, so the gallery has to be inert rather than absent.
/// </summary>
[TestFixture]
internal sealed class StandalonePictureTests
{
    private static readonly WebLink Picture = new("https://example.com/chart.png");

    private static ImageViewerViewModel Viewer(IImageLoader? loader = null) =>
        new(Picture, "a chart", loader ?? Substitute.For<IImageLoader>(), () => { });

    [Test]
    public void ItShowsThePictureAndItsCaption()
    {
        using ImageViewerViewModel viewer = Viewer();

        Assert.Multiple(() =>
        {
            Assert.That(viewer.Link, Is.EqualTo(Picture));
            Assert.That(viewer.Title, Is.EqualTo("a chart"));
            Assert.That(viewer.Summary, Is.Null, "there is no post behind it");
            Assert.That(viewer.CommunityLabel, Is.Empty);
        });
    }

    [Test]
    public void ThereIsNowhereToFlickTo()
    {
        using ImageViewerViewModel viewer = Viewer();

        Assert.Multiple(() =>
        {
            Assert.That(viewer.CanShowNext, Is.False);
            Assert.That(viewer.CanShowPrevious, Is.False);
            Assert.That(viewer.PositionLabel, Is.Empty);
        });
    }

    [Test]
    public async Task MovingDoesNothingRatherThanFailing()
    {
        using ImageViewerViewModel viewer = Viewer();

        await viewer.ShowNextCommand.ExecuteAsync(null);
        await viewer.ShowPreviousCommand.ExecuteAsync(null);

        Assert.That(viewer.Link, Is.EqualTo(Picture));
    }

    [Test]
    public async Task TheShellOpensOneOnRequest()
    {
        var services = new TestServices();
        using var shell = new MainViewModel(services.Services, AppSettings.Default);

        shell.ShowPicture(Picture, "a chart");

        Assert.Multiple(() =>
        {
            Assert.That(shell.ImageViewer, Is.Not.Null);
            Assert.That(shell.ImageViewer!.Title, Is.EqualTo("a chart"));
            Assert.That(shell.ImageViewer.Summary, Is.Null);
        });

        await Task.CompletedTask;
    }

    [Test]
    public void AnUnusablePictureOpensNothing()
    {
        var services = new TestServices();
        using var shell = new MainViewModel(services.Services, AppSettings.Default);

        shell.ShowPicture(default, "nothing");

        Assert.That(shell.ImageViewer, Is.Null);
    }
}
