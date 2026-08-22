using System.Collections.Immutable;
using Lemmy.Api.Dto;
using Lemmy.Domain;
using Lemmy.Domain.Models;

namespace Lemmy.Api;

/// <summary>
/// Rebuilds a comment thread from the flat list Lemmy sends. The server's ordering within each
/// level is honoured exactly — it already applied the requested sort — and only the nesting is
/// reconstructed, from each comment's <see cref="CommentPath"/>.
/// </summary>
internal static class CommentTreeBuilder
{
    internal static CommentThread Build(ImmutableArray<CommentViewWire> wires)
    {
        if (wires.IsEmpty)
        {
            return CommentThread.Empty;
        }

        var items = new List<Item>(wires.Length);
        var indexById = new Dictionary<CommentId, int>(wires.Length);

        foreach (CommentViewWire wire in wires)
        {
            if (!WireMapper.TryMapComment(wire.Comment, out Comment comment)
                || !WireMapper.TryMapPerson(wire.Creator, out Person creator))
            {
                continue;
            }

            // A duplicate id would corrupt the tree; the first copy wins.
            if (!indexById.TryAdd(comment.Id, items.Count))
            {
                continue;
            }

            items.Add(new Item(
                comment,
                creator,
                WireMapper.MapTally(wire.Counts),
                wire.CreatorIsModerator,
                wire.CreatorIsAdmin,
                wire.MyVote.ToVote()));
        }

        if (items.Count == 0)
        {
            return CommentThread.Empty;
        }

        var childIndices = new Dictionary<int, List<int>>();
        var rootIndices = new List<int>();

        for (int index = 0; index < items.Count; index++)
        {
            CommentId? parentId = items[index].Comment.Path.ParentId;

            // A parent outside this response means we fetched a sub-thread; its top is our root.
            if (parentId is { } parent && indexById.TryGetValue(parent, out int parentIndex))
            {
                if (!childIndices.TryGetValue(parentIndex, out List<int>? siblings))
                {
                    siblings = [];
                    childIndices[parentIndex] = siblings;
                }

                siblings.Add(index);
            }
            else
            {
                rootIndices.Add(index);
            }
        }

        var roots = ImmutableArray.CreateBuilder<CommentNode>(rootIndices.Count);
        foreach (int rootIndex in rootIndices)
        {
            roots.Add(Materialise(rootIndex, items, childIndices));
        }

        return new CommentThread(roots.ToImmutable());
    }

    private static CommentNode Materialise(int index, List<Item> items, Dictionary<int, List<int>> childIndices)
    {
        Item item = items[index];

        ImmutableArray<CommentNode> replies;
        if (childIndices.TryGetValue(index, out List<int>? children))
        {
            var builder = ImmutableArray.CreateBuilder<CommentNode>(children.Count);
            foreach (int child in children)
            {
                builder.Add(Materialise(child, items, childIndices));
            }

            replies = builder.ToImmutable();
        }
        else
        {
            replies = [];
        }

        return new CommentNode(
            item.Comment, item.Creator, item.Tally, item.IsModerator, item.IsAdmin, replies, item.MyVote);
    }

    private readonly record struct Item(
        Comment Comment,
        Person Creator,
        CommentTally Tally,
        bool IsModerator,
        bool IsAdmin,
        Vote MyVote);
}
