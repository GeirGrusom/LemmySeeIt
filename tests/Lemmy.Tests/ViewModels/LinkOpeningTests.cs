using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.Tests.TestSupport;
using Lemmy.ViewModels;

namespace Lemmy.Tests.ViewModels;

[TestFixture]
internal sealed class LinkOpeningTests
{
    private TestServices services = null!;
    private RecordingNavigator navigator = null!;

    [SetUp]
    public void CreateServices()
    {
        services = new TestServices();
        navigator = new RecordingNavigator();
    }

    private PostDetailViewModel CreatePage(PostSummary summary) =>
        new(services.Services, navigator, services.Api, summary, AppSettings.Default);

    private static PostSummary LinkPost() =>
        Sample.PostSummary() with { Post = Sample.Post(url: "https://apnews.com/article/x", contentType: "text/html") };

    [Test]
    public async Task OpeningALinkHandsItToThePlatform()
    {
        using PostDetailViewModel page = CreatePage(LinkPost());

        await page.OpenLinkCommand.ExecuteAsync(null);

        await services.LinkOpener.Received(1).OpenAsync(
            Arg.Is<WebLink>(link => link.Value == "https://apnews.com/article/x"));
    }

    [Test]
    public void ASelfPostHasNoLinkToOpen()
    {
        using PostDetailViewModel page = CreatePage(Sample.PostSummary());

        Assert.Multiple(() =>
        {
            Assert.That(page.HasLink, Is.False);
            Assert.That(page.OpenLinkCommand.CanExecute(null), Is.False);
        });
    }

    /// <summary>
    /// The one thing a read-only client cannot do is take part, so getting to the post on its own
    /// instance — where voting and commenting live — is worth a control of its own.
    /// </summary>
    [Test]
    public async Task APostCanBeOpenedOnItsOwnInstance()
    {
        PostSummary summary = LinkPost();
        using PostDetailViewModel page = CreatePage(summary);

        await page.OpenOnTheWebCommand.ExecuteAsync(null);

        await services.LinkOpener.Received(1).OpenAsync(
            Arg.Is<WebLink>(link => link.Value == summary.Post.ActorId.Value));
    }

    [Test]
    public void OpeningTheWebAddressIsOfferedForASelfPostToo()
    {
        using PostDetailViewModel page = CreatePage(Sample.PostSummary());

        Assert.That(page.OpenOnTheWebCommand.CanExecute(null), Is.True);
    }

    /// <summary>A link that will not open is a disappointment, not something to crash over.</summary>
    [Test]
    public async Task ARefusedLaunchIsSwallowed()
    {
        services.LinkOpener.OpenAsync(Arg.Any<WebLink>()).Returns(Task.FromResult(false));
        using PostDetailViewModel page = CreatePage(LinkPost());

        Assert.That(async () => await page.OpenLinkCommand.ExecuteAsync(null), Throws.Nothing);
        await Task.CompletedTask;
    }

    [Test]
    public void TheOpenerDoesNothingWithoutAWindowToLaunchFrom()
    {
        var opener = new SystemLinkOpener();

        Assert.That(async () => await opener.OpenAsync(WebLink.Parse("https://example.com")), Throws.Nothing);
    }

    [Test]
    public async Task TheOpenerRefusesADefaultLink()
    {
        var opener = new SystemLinkOpener();

        Assert.That(await opener.OpenAsync(default), Is.False);
    }
}
