using System.Collections.Immutable;

namespace Lemmy.Domain.Models;

/// <summary>A post's comments, assembled into trees.</summary>
/// <param name="Roots">The top-level comments, each carrying its own replies.</param>
public sealed record CommentThread(ImmutableArray<CommentNode> Roots)
{
    /// <summary>A post with no comments.</summary>
    public static CommentThread Empty { get; } = new([]);

    /// <summary>Every comment in the thread, parents before their replies.</summary>
    public ImmutableArray<CommentNode> Flatten()
    {
        var builder = ImmutableArray.CreateBuilder<CommentNode>();
        foreach (CommentNode root in Roots)
        {
            Append(root, builder);
        }

        return builder.ToImmutable();
    }

    private static void Append(CommentNode node, ImmutableArray<CommentNode>.Builder builder)
    {
        builder.Add(node);
        foreach (CommentNode reply in node.Replies)
        {
            Append(reply, builder);
        }
    }
}
