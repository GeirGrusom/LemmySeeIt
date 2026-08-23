using System.Collections.Immutable;
using Lemmy.Api.Dto;
using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Api;

/// <summary>
/// Turns wire records into domain models. Every entry point is a <c>Try</c> method: a single post
/// with an unparseable field should drop out of the feed, not take the whole page down with it.
/// </summary>
internal static class WireMapper
{
    internal static bool TryMapPerson(PersonWire? wire, out Person person)
    {
        person = null!;

        if (wire is null
            || !Username.TryParse(wire.Name.AsSpan(), out Username name)
            || !ActorId.TryParse(wire.ActorId.AsSpan(), out ActorId actorId)
            || !PersonId.TryCreate(wire.Id, out PersonId id))
        {
            return false;
        }

        person = new Person(
            id,
            name,
            string.IsNullOrWhiteSpace(wire.DisplayName) ? null : wire.DisplayName,
            actorId,
            TryLink(wire.Avatar),
            wire.Local,
            wire.BotAccount,
            wire.Banned,
            wire.Deleted,
            wire.Published);

        return true;
    }

    internal static bool TryMapCommunity(CommunityWire? wire, out Community community)
    {
        community = null!;

        if (wire is null
            || !CommunityName.TryParse(wire.Name.AsSpan(), out CommunityName name)
            || !ActorId.TryParse(wire.ActorId.AsSpan(), out ActorId actorId)
            || !CommunityId.TryCreate(wire.Id, out CommunityId id))
        {
            return false;
        }

        MarkdownText.TryCreate(wire.Description.AsSpan(), out MarkdownText description);

        community = new Community(
            id,
            name,
            wire.Title ?? name.Value,
            description,
            actorId,
            TryLink(wire.Icon),
            TryLink(wire.Banner),
            wire.Local,
            wire.Nsfw,
            wire.Removed,
            wire.Deleted,
            wire.Published);

        return true;
    }

    internal static bool TryMapPost(PostWire? wire, out Post post)
    {
        post = null!;

        if (wire is null
            || !PostId.TryCreate(wire.Id, out PostId id)
            || !PersonId.TryCreate(wire.CreatorId, out PersonId creatorId)
            || !CommunityId.TryCreate(wire.CommunityId, out CommunityId communityId)
            || !ActorId.TryParse(wire.ApId.AsSpan(), out ActorId actorId)
            || PostTitle.CreateTruncated(wire.Name.AsSpan()) is not { } title)
        {
            return false;
        }

        MarkdownText.TryCreate(wire.Body.AsSpan(), out MarkdownText body);
        LanguageId.TryCreate(wire.LanguageId, out LanguageId language);

        post = new Post(
            id,
            title,
            body,
            TryLink(wire.Url),
            TryLink(wire.ThumbnailUrl),
            MediaType.TryParse(wire.UrlContentType.AsSpan(), out MediaType mediaType) ? mediaType : null,
            string.IsNullOrWhiteSpace(wire.EmbedTitle) ? null : wire.EmbedTitle,
            string.IsNullOrWhiteSpace(wire.EmbedDescription) ? null : wire.EmbedDescription,
            creatorId,
            communityId,
            actorId,
            language,
            wire.Nsfw,
            wire.Locked,
            wire.Removed,
            wire.Deleted,
            wire.FeaturedCommunity,
            wire.FeaturedLocal,
            wire.Published,
            wire.Updated);

        return true;
    }

    internal static bool TryMapComment(CommentWire? wire, out Comment comment)
    {
        comment = null!;

        if (wire is null
            || !CommentId.TryCreate(wire.Id, out CommentId id)
            || !PostId.TryCreate(wire.PostId, out PostId postId)
            || !PersonId.TryCreate(wire.CreatorId, out PersonId creatorId)
            || !ActorId.TryParse(wire.ApId.AsSpan(), out ActorId actorId)
            || !CommentPath.TryParse(wire.Path.AsSpan(), out CommentPath path))
        {
            return false;
        }

        MarkdownText.TryCreate(wire.Content.AsSpan(), out MarkdownText content);
        LanguageId.TryCreate(wire.LanguageId, out LanguageId language);

        comment = new Comment(
            id,
            postId,
            creatorId,
            content,
            path,
            actorId,
            language,
            wire.Removed,
            wire.Deleted,
            wire.Distinguished,
            wire.Published,
            wire.Updated);

        return true;
    }

    internal static bool TryMapPostSummary(PostViewWire? wire, out PostSummary summary)
    {
        summary = null!;

        if (wire is null
            || !TryMapPost(wire.Post, out Post post)
            || !TryMapPerson(wire.Creator, out Person creator)
            || !TryMapCommunity(wire.Community, out Community community))
        {
            return false;
        }

        summary = new PostSummary(
            post,
            creator,
            community,
            MapTally(wire.Counts),
            wire.CreatorIsModerator,
            wire.CreatorIsAdmin,
            wire.MyVote.ToVote(),
            wire.Subscribed.ToSubscriptionState());
        return true;
    }

    internal static bool TryMapCommunitySummary(CommunityViewWire? wire, out CommunitySummary summary)
    {
        summary = null!;

        if (wire is null || !TryMapCommunity(wire.Community, out Community community))
        {
            return false;
        }

        CommunityAggregatesWire? counts = wire.Counts;
        var tally = new CommunityTally(
            VoteCount.Clamp(counts?.Subscribers ?? 0),
            VoteCount.Clamp(counts?.Posts ?? 0),
            VoteCount.Clamp(counts?.Comments ?? 0),
            VoteCount.Clamp(counts?.UsersActiveMonth ?? 0));

        summary = new CommunitySummary(community, tally, wire.Subscribed.ToSubscriptionState());
        return true;
    }

    /// <summary>
    /// Reads the counts a vote response came back with. Both like endpoints answer with the whole
    /// view again, but only the tally and the account's own vote can have changed, so that is all
    /// this takes — the caller already has the rest on screen.
    /// </summary>
    /// <summary>
    /// Maps a single comment view — what the write endpoints answer with. The replies are empty
    /// because the response carries none, which is the truth for a comment just posted and is why
    /// editing and deleting take <see cref="TryMapComment"/> instead: those have replies already, and
    /// an empty list would throw them away.
    /// </summary>
    internal static bool TryMapCommentNode(CommentViewWire? wire, out CommentNode node)
    {
        node = null!;

        if (wire is null
            || !TryMapComment(wire.Comment, out Comment comment)
            || !TryMapPerson(wire.Creator, out Person creator))
        {
            return false;
        }

        node = new CommentNode(
            comment,
            creator,
            MapTally(wire.Counts),
            wire.CreatorIsModerator,
            wire.CreatorIsAdmin,
            [],
            wire.MyVote.ToVote());
        return true;
    }

    /// <summary>
    /// Maps an account's own page. The comments come back as a flat list of what they wrote rather
    /// than as threads, so each is mapped on its own with no replies under it — which is the truth
    /// here, not a simplification.
    /// </summary>
    internal static bool TryMapPersonProfile(GetPersonDetailsResponse response, out PersonProfile profile)
    {
        profile = null!;

        PersonViewWire? view = response.PersonView;
        if (view is null || !TryMapPerson(view.Person, out Person person))
        {
            return false;
        }

        MarkdownText.TryCreate((view.Person?.Bio).AsSpan(), out MarkdownText bio);

        var comments = ImmutableArray.CreateBuilder<CommentNode>(response.Comments.Length);
        foreach (CommentViewWire wire in response.Comments)
        {
            if (TryMapCommentNode(wire, out CommentNode node))
            {
                comments.Add(node);
            }
        }

        profile = new PersonProfile(
            person,
            bio,
            TryLink(view.Person?.Banner),
            new PersonTally(
                VoteCount.Clamp(view.Counts?.PostCount ?? 0),
                VoteCount.Clamp(view.Counts?.CommentCount ?? 0)),
            view.IsAdmin,
            MapPostSummaries(response.Posts),
            comments.ToImmutable());
        return true;
    }

    internal static VoteOutcome MapVoteOutcome(PostViewWire? wire)
    {
        PostTally tally = MapTally(wire?.Counts);
        return new VoteOutcome(wire?.MyVote.ToVote() ?? Vote.None, tally.Score, tally.Upvotes, tally.Downvotes);
    }

    /// <inheritdoc cref="MapVoteOutcome(PostViewWire?)" />
    internal static VoteOutcome MapVoteOutcome(CommentViewWire? wire)
    {
        CommentTally tally = MapTally(wire?.Counts);
        return new VoteOutcome(wire?.MyVote.ToVote() ?? Vote.None, tally.Score, tally.Upvotes, tally.Downvotes);
    }

    internal static PostTally MapTally(PostAggregatesWire? wire) =>
        wire is null
            ? PostTally.Empty
            : new PostTally(
                new Score(wire.Score),
                VoteCount.Clamp(wire.Upvotes),
                VoteCount.Clamp(wire.Downvotes),
                VoteCount.Clamp(wire.Comments),
                wire.NewestCommentTime);

    internal static CommentTally MapTally(CommentAggregatesWire? wire) =>
        wire is null
            ? CommentTally.Empty
            : new CommentTally(
                new Score(wire.Score),
                VoteCount.Clamp(wire.Upvotes),
                VoteCount.Clamp(wire.Downvotes),
                VoteCount.Clamp(wire.ChildCount));

    /// <summary>
    /// Reads the signed-in account from a site response. Absent means the token was missing or no
    /// longer works — Lemmy answers 200 either way and simply leaves this out.
    /// </summary>
    internal static bool TryMapAccount(GetSiteResponse response, InstanceAddress address, out Account account)
    {
        account = null!;

        if (response.MyUser?.LocalUserView?.Person is not { } wire || !TryMapPerson(wire, out Person person))
        {
            return false;
        }

        LocalUserWire preferences = response.MyUser.LocalUserView.LocalUser ?? new LocalUserWire();

        account = new Account(
            person.Id,
            person.Name,
            person.DisplayName,
            address,
            person.Avatar,
            preferences.ShowNsfw,
            preferences.BlurNsfw);

        return true;
    }

    internal static SiteSummary MapSite(GetSiteResponse response, InstanceAddress address)
    {
        SiteWire? site = response.SiteView?.Site;
        SiteAggregatesWire? counts = response.SiteView?.Counts;

        LocalSiteWire? local = response.SiteView?.LocalSite;

        MarkdownText.TryCreate((site?.Sidebar).AsSpan(), out MarkdownText sidebar);
        MarkdownText.TryCreate((local?.ApplicationQuestion).AsSpan(), out MarkdownText question);

        return new SiteSummary(
            address,
            string.IsNullOrWhiteSpace(site?.Name) ? address.Value : site.Name,
            string.IsNullOrWhiteSpace(site?.Description) ? null : site.Description,
            sidebar,
            TryLink(site?.Icon),
            TryLink(site?.Banner),
            string.IsNullOrWhiteSpace(response.Version) ? null : response.Version,
            VoteCount.Clamp(counts?.Users ?? 0),
            VoteCount.Clamp(counts?.Communities ?? 0),
            local?.RegistrationMode.ToRegistrationMode() ?? Domain.RegistrationMode.Unknown,
            question,
            local?.RequireEmailVerification ?? false);
    }

    internal static ImmutableArray<PostSummary> MapPostSummaries(ImmutableArray<PostViewWire> wires)
    {
        var builder = ImmutableArray.CreateBuilder<PostSummary>(wires.Length);
        foreach (PostViewWire wire in wires)
        {
            if (TryMapPostSummary(wire, out PostSummary summary))
            {
                builder.Add(summary);
            }
        }

        return builder.ToImmutable();
    }

    internal static ImmutableArray<CommunitySummary> MapCommunitySummaries(ImmutableArray<CommunityViewWire> wires)
    {
        var builder = ImmutableArray.CreateBuilder<CommunitySummary>(wires.Length);
        foreach (CommunityViewWire wire in wires)
        {
            if (TryMapCommunitySummary(wire, out CommunitySummary summary))
            {
                builder.Add(summary);
            }
        }

        return builder.ToImmutable();
    }

    internal static ImmutableArray<Person> MapPeople(ImmutableArray<PersonViewWire> wires)
    {
        var builder = ImmutableArray.CreateBuilder<Person>(wires.Length);
        foreach (PersonViewWire wire in wires)
        {
            if (TryMapPerson(wire.Person, out Person person))
            {
                builder.Add(person);
            }
        }

        return builder.ToImmutable();
    }

    private static WebLink? TryLink(string? value) =>
        WebLink.TryParse(value.AsSpan(), out WebLink link) ? link : null;
}
