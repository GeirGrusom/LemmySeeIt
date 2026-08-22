namespace Lemmy.Domain.Markdown;

/// <summary>How a run of text is styled. Combinable, because Markdown nests emphasis freely.</summary>
[Flags]
public enum MarkdownSpanStyle
{
    /// <summary>Plain body text.</summary>
    None = 0,

    /// <summary><c>**bold**</c>.</summary>
    Bold = 1,

    /// <summary><c>*italic*</c>.</summary>
    Italic = 2,

    /// <summary><c>~~struck through~~</c>.</summary>
    Strikethrough = 4,

    /// <summary><c>`code`</c>.</summary>
    Code = 8,

    /// <summary><c>^superscript^</c>, which Lemmy enables.</summary>
    Superscript = 16,

    /// <summary><c>~subscript~</c>, which Lemmy enables.</summary>
    Subscript = 32,
}
