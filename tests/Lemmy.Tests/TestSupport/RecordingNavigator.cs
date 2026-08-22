using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;
using Lemmy.ViewModels;

namespace Lemmy.Tests.TestSupport;

/// <summary>A navigator that records where a page tried to go instead of going there.</summary>
internal sealed class RecordingNavigator : INavigator
{
    private readonly List<PageViewModel> pushed = [];

    internal IReadOnlyList<PageViewModel> Pushed => pushed;

    internal int PopCount { get; private set; }

    internal List<PostSummary> ImagesShown { get; } = [];

    internal IImageGallery? LastGallery { get; private set; }

    internal List<(WebLink Picture, string Caption)> PicturesShown { get; } = [];

    public bool CanPop => pushed.Count > 0;

    public void Push(PageViewModel page) => pushed.Add(page);

    public void Pop() => PopCount++;

    public void ShowImage(PostSummary summary, IImageGallery gallery)
    {
        ImagesShown.Add(summary);
        LastGallery = gallery;
    }

    public void ShowPicture(WebLink picture, string caption) => PicturesShown.Add((picture, caption));
}
