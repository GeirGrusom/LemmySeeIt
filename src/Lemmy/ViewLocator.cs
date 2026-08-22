using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Lemmy.ViewModels;
using Lemmy.Views;

namespace Lemmy;

/// <summary>
/// Maps a page view model to its view. The mapping is an explicit switch rather than the usual
/// name-based reflection lookup: reflection would be trimmed away in a Native AOT build, and every
/// view here is known at compile time anyway.
/// </summary>
/// <remarks>
/// Views are rebuilt on each navigation rather than cached. What the reader would notice losing —
/// their place in the feed — is preserved by <see cref="Views.ScrollMemory"/> instead, and
/// everything else on screen is bound to the page, which outlives the control.
/// </remarks>
public sealed class ViewLocator : IDataTemplate
{
    /// <inheritdoc />
    public Control Build(object? param) => param switch
    {
        FeedViewModel => new FeedView(),
        PostDetailViewModel => new PostDetailView(),
        CommunitiesViewModel => new CommunitiesView(),
        SearchViewModel => new SearchView(),
        _ => new TextBlock { Text = $"No view for {param?.GetType().Name ?? "null"}." },
    };

    /// <inheritdoc />
    public bool Match(object? data) => data is PageViewModel;
}
