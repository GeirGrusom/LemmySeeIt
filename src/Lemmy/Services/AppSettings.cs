using System.Collections.Immutable;
using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Services;

/// <summary>
/// Everything the app remembers between runs. Immutable: settings changes flow through
/// <c>with</c> expressions, so a half-applied change can never be what gets written to disk.
/// </summary>
/// <param name="Instance">The server to read from.</param>
/// <param name="Listing">Which slice of the instance the feed shows.</param>
/// <param name="Sort">How the feed is ordered.</param>
/// <param name="CommentSort">How comment threads are ordered.</param>
/// <param name="ShowNsfw">Whether to include content flagged not safe for work.</param>
/// <param name="BlurNsfwImages">Whether to blur such images until tapped, when they are shown at all.</param>
/// <param name="RecentInstances">
/// Servers the reader has actually used, most recent first and including the current one.
/// </param>
public sealed record AppSettings(
    InstanceAddress Instance,
    ListingType Listing = ListingType.All,
    PostSortType Sort = PostSortType.Active,
    CommentSortType CommentSort = CommentSortType.Hot,
    bool ShowNsfw = false,
    bool BlurNsfwImages = true,
    ImmutableArray<InstanceAddress> RecentInstances = default)
{
    /// <summary>How many servers to remember. Long enough to cover moving between a few, short
    /// enough that the picker stays a list rather than a history.</summary>
    public const int MaxRecentInstances = 8;

    /// <summary>The remembered servers, never <see langword="default"/>.</summary>
    public ImmutableArray<InstanceAddress> Recent =>
        RecentInstances.IsDefault ? [] : RecentInstances;

    /// <summary>
    /// The same settings with <paramref name="instance"/> at the front of the remembered list, and
    /// that instance selected. Moving back to a server already remembered promotes it rather than
    /// adding it twice.
    /// </summary>
    public AppSettings WithInstance(InstanceAddress instance)
    {
        if (!instance.IsValid)
        {
            return this;
        }

        ImmutableArray<InstanceAddress>.Builder builder = ImmutableArray.CreateBuilder<InstanceAddress>();
        builder.Add(instance);

        foreach (InstanceAddress remembered in Recent)
        {
            if (remembered != instance && builder.Count < MaxRecentInstances)
            {
                builder.Add(remembered);
            }
        }

        return this with { Instance = instance, RecentInstances = builder.ToImmutable() };
    }

    /// <summary>
    /// Compares by value, including the remembered servers.
    /// </summary>
    /// <remarks>
    /// The compiler's own version would not. A record compares each member with
    /// <see cref="EqualityComparer{T}.Default"/>, and for <see cref="ImmutableArray{T}"/> that is
    /// reference equality on the array behind it — so two settings holding the same servers in the
    /// same order would compare unequal. This type is compared with <c>==</c> to decide whether a
    /// change is worth saving and re-applying, so that would mean writing the file and rebuilding
    /// every page over a difference that does not exist.
    /// </remarks>
    public bool Equals(AppSettings? other) =>
        other is not null
        && Instance == other.Instance
        && Listing == other.Listing
        && Sort == other.Sort
        && CommentSort == other.CommentSort
        && ShowNsfw == other.ShowNsfw
        && BlurNsfwImages == other.BlurNsfwImages
        && Recent.AsSpan().SequenceEqual(other.Recent.AsSpan());

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(Instance);
        hash.Add(Listing);
        hash.Add(Sort);
        hash.Add(CommentSort);
        hash.Add(ShowNsfw);
        hash.Add(BlurNsfwImages);

        foreach (InstanceAddress address in Recent)
        {
            hash.Add(address);
        }

        return hash.ToHashCode();
    }

    /// <summary>What a first run uses: a large general-purpose instance, safe defaults.</summary>
    public static AppSettings Default { get; } = new(InstanceAddress.Parse("lemmy.world"));
}
