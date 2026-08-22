using System.Collections.Immutable;
using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Services;

namespace Lemmy.Tests.TestSupport;

/// <summary>
/// Assembles an <see cref="AppServices"/> whose every dependency is a substitute, so a view-model
/// test exercises the view model and nothing else.
/// </summary>
internal sealed class TestServices
{
    internal TestServices()
    {
        Api = Substitute.For<ILemmyApi>();
        Api.Instance.Returns(Sample.Instance);

        // Every endpoint answers with something usable by default. Without this a substitute hands
        // back default(ImmutableArray<T>), which throws the moment anything enumerates it — an
        // failure that looks like a product bug and is not one.
        Api.GetFeedAsync(Arg.Any<FeedQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PostPage.Empty));
        Api.GetCommentsAsync(Arg.Any<CommentQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CommentThread.Empty));
        Api.GetCommunitiesAsync(Arg.Any<CommunityQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ImmutableArray<CommunitySummary>.Empty));
        Api.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SearchResults.Empty));

        ApiFactory = Substitute.For<ILemmyApiFactory>();
        ApiFactory.Create(Arg.Any<InstanceAddress>(), Arg.Any<SessionToken>()).Returns(Api);

        ImageLoader = Substitute.For<IImageLoader>();
        SettingsStore = Substitute.For<IAppSettingsStore>();
        SettingsStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(AppSettings.Default));

        LinkOpener = Substitute.For<ILinkOpener>();
        LinkOpener.OpenAsync(Arg.Any<WebLink>()).Returns(Task.FromResult(true));

        SessionStore = new MemorySessionStore();

        Clock = FixedTimeProvider.AtReference();
        Subscriptions = new SubscriptionTracker();
        Services = new AppServices(
            ApiFactory, ImageLoader, SettingsStore, LinkOpener, SessionStore, Clock, Subscriptions);
    }

    internal SubscriptionTracker Subscriptions { get; }

    internal ILemmyApi Api { get; }

    internal ILemmyApiFactory ApiFactory { get; }

    internal IImageLoader ImageLoader { get; }

    internal IAppSettingsStore SettingsStore { get; }

    internal ILinkOpener LinkOpener { get; }

    internal ISessionStore SessionStore { get; }

    internal FixedTimeProvider Clock { get; }

    internal AppServices Services { get; }

    /// <summary>Makes the feed endpoint answer with the given pages, one per call.</summary>
    internal void FeedReturns(params PostPage[] pages)
    {
        if (pages.Length == 1)
        {
            Api.GetFeedAsync(Arg.Any<FeedQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(pages[0]));
            return;
        }

        int index = 0;
        Api.GetFeedAsync(Arg.Any<FeedQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(pages[Math.Min(index++, pages.Length - 1)]));
    }
}
