using System.Collections.Immutable;
using System.Globalization;

namespace Lemmy.Domain;

/// <summary>
/// The materialised path Lemmy stores on every comment: a dot-separated chain of ancestor ids
/// rooted at a literal <c>0</c>, e.g. <c>0.25414623.25414700</c>. It is the only thing in the wire
/// format that says how comments nest, so parsing it correctly is what makes a thread a tree.
/// </summary>
public readonly record struct CommentPath
{
    private const char Separator = '.';

    private readonly string? path;

    /// <summary>Validates a comment path.</summary>
    /// <exception cref="DomainValidationException">The text is not a rooted dot-separated id chain.</exception>
    public CommentPath(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsLegal(value.AsSpan(), out string? reason))
        {
            throw DomainValidationException.For(nameof(CommentPath), reason!);
        }

        path = value;
    }

    /// <summary>The path as Lemmy stores it.</summary>
    public string Value => path ?? string.Empty;

    /// <summary><see langword="false"/> for <see langword="default"/>.</summary>
    public bool IsValid => path is not null;

    /// <summary>
    /// How deeply the comment is nested. A direct reply to the post is depth 1, a reply to that is
    /// depth 2. The synthetic <c>0</c> root is not counted.
    /// </summary>
    public int Depth
    {
        get
        {
            if (path is null)
            {
                return 0;
            }

            int separators = 0;
            foreach (char character in path.AsSpan())
            {
                if (character == Separator)
                {
                    separators++;
                }
            }

            return separators;
        }
    }

    /// <summary><see langword="true"/> when the comment replies to the post rather than to a comment.</summary>
    public bool IsTopLevel => Depth <= 1;

    /// <summary>The comment this one replies to, or <see langword="null"/> at the top level.</summary>
    public CommentId? ParentId
    {
        get
        {
            if (path is null)
            {
                return null;
            }

            ReadOnlySpan<char> span = path.AsSpan();
            int lastSeparator = span.LastIndexOf(Separator);
            if (lastSeparator <= 0)
            {
                return null;
            }

            ReadOnlySpan<char> ancestors = span[..lastSeparator];
            int parentStart = ancestors.LastIndexOf(Separator) + 1;

            return CommentId.TryParse(ancestors[parentStart..], out CommentId parent) ? parent : null;
        }
    }

    /// <summary>The comment at the top of this one's thread; equals the comment itself at depth 1.</summary>
    public CommentId? RootId
    {
        get
        {
            if (path is null)
            {
                return null;
            }

            ReadOnlySpan<char> span = path.AsSpan();
            int firstSeparator = span.IndexOf(Separator);
            if (firstSeparator < 0)
            {
                return null;
            }

            ReadOnlySpan<char> rest = span[(firstSeparator + 1)..];
            int nextSeparator = rest.IndexOf(Separator);
            ReadOnlySpan<char> root = nextSeparator < 0 ? rest : rest[..nextSeparator];

            return CommentId.TryParse(root, out CommentId rootId) ? rootId : null;
        }
    }

    /// <summary>
    /// The ancestor chain, outermost first, excluding the synthetic root and the comment itself.
    /// Empty for a top-level comment.
    /// </summary>
    public ImmutableArray<CommentId> Ancestors
    {
        get
        {
            int depth = Depth;
            if (depth <= 1)
            {
                return [];
            }

            var builder = ImmutableArray.CreateBuilder<CommentId>(depth - 1);
            ReadOnlySpan<char> remaining = path.AsSpan();

            // Skip the synthetic "0" root.
            remaining = remaining[(remaining.IndexOf(Separator) + 1)..];

            while (true)
            {
                int separator = remaining.IndexOf(Separator);
                if (separator < 0)
                {
                    // What is left is the comment itself, not an ancestor.
                    break;
                }

                if (CommentId.TryParse(remaining[..separator], out CommentId ancestor))
                {
                    builder.Add(ancestor);
                }

                remaining = remaining[(separator + 1)..];
            }

            return builder.ToImmutable();
        }
    }

    /// <summary>Validates a comment path without throwing.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out CommentPath result)
    {
        ReadOnlySpan<char> trimmed = text.Trim();
        if (!IsLegal(trimmed, out _))
        {
            result = default;
            return false;
        }

        result = new CommentPath(trimmed.ToString());
        return true;
    }

    /// <summary>Builds the path a reply to <paramref name="parent"/> would have.</summary>
    public CommentPath Append(CommentId child)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(child.Value, 1);

        string suffix = child.Value.ToString(CultureInfo.InvariantCulture);
        return new CommentPath(string.Concat(Value.AsSpan(), stackalloc char[] { Separator }, suffix.AsSpan()));
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    private static bool IsLegal(ReadOnlySpan<char> value, out string? reason)
    {
        if (value.IsEmpty)
        {
            reason = "the path is empty";
            return false;
        }

        if (value[0] != '0' || (value.Length > 1 && value[1] != Separator))
        {
            reason = $"'{value.ToString()}' does not start at the synthetic '0' root";
            return false;
        }

        ReadOnlySpan<char> remaining = value;
        while (!remaining.IsEmpty)
        {
            int separator = remaining.IndexOf(Separator);
            ReadOnlySpan<char> segment = separator < 0 ? remaining : remaining[..separator];

            if (segment.IsEmpty)
            {
                reason = $"'{value.ToString()}' has an empty segment";
                return false;
            }

            foreach (char character in segment)
            {
                if (!char.IsAsciiDigit(character))
                {
                    reason = $"'{value.ToString()}' has a non-numeric segment";
                    return false;
                }
            }

            if (separator < 0)
            {
                break;
            }

            remaining = remaining[(separator + 1)..];

            if (remaining.IsEmpty)
            {
                reason = $"'{value.ToString()}' ends with a separator";
                return false;
            }
        }

        reason = null;
        return true;
    }
}
