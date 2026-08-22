namespace Lemmy.Domain;

/// <summary>
/// A query someone typed into the search box, trimmed and length-checked. Searching for whitespace
/// is a request every server answers slowly and uselessly, so it is rejected here instead.
/// </summary>
public readonly record struct SearchTerm
{
    /// <summary>Below this, a search matches so much that the results are noise.</summary>
    public const int MinLength = 2;

    /// <summary>Longer than this and no server will do anything useful with it.</summary>
    public const int MaxLength = 256;

    private readonly string? term;

    /// <summary>Validates a search term.</summary>
    /// <exception cref="DomainValidationException">The term is too short or too long once trimmed.</exception>
    public SearchTerm(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsLegal(value.AsSpan(), out string? reason))
        {
            throw DomainValidationException.For(nameof(SearchTerm), reason!);
        }

        term = value.Trim();
    }

    /// <summary>The trimmed query text.</summary>
    public string Value => term ?? string.Empty;

    /// <summary><see langword="false"/> for <see langword="default"/>.</summary>
    public bool IsValid => term is not null;

    /// <summary>Validates a search term without throwing — the right call for a text box.</summary>
    public static bool TryCreate(ReadOnlySpan<char> value, out SearchTerm searchTerm)
    {
        ReadOnlySpan<char> trimmed = value.Trim();
        if (!IsLegal(trimmed, out _))
        {
            searchTerm = default;
            return false;
        }

        searchTerm = new SearchTerm(trimmed.ToString());
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    private static bool IsLegal(ReadOnlySpan<char> value, out string? reason)
    {
        ReadOnlySpan<char> trimmed = value.Trim();

        if (trimmed.Length < MinLength)
        {
            reason = $"the term must be at least {MinLength} characters once trimmed";
            return false;
        }

        if (trimmed.Length > MaxLength)
        {
            reason = $"the term is {trimmed.Length} characters, over the {MaxLength} limit";
            return false;
        }

        reason = null;
        return true;
    }
}
