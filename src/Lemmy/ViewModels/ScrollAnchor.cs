namespace Lemmy.ViewModels;

/// <summary>
/// Where a scrolling list was left, expressed as a row rather than as a pixel offset.
/// <para>
/// A virtualising panel does not know how tall the rows it has not built yet are, so its scroll
/// extent is an estimate that shifts as more rows are realised. A pixel offset therefore means
/// something slightly different before and after a navigation, and the reader comes back near where
/// they were rather than at it. The row index does not drift.
/// </para>
/// </summary>
/// <param name="Index">The first row that was visible.</param>
/// <param name="Delta">
/// How far that row's top sat above the top of the viewport, so a row scrolled half out of view
/// comes back half out of view. Zero or negative in practice.
/// </param>
public readonly record struct ScrollAnchor(int Index, double Delta)
{
    /// <summary>The top of the list, which is where a page starts.</summary>
    public static ScrollAnchor Top => default;

    /// <summary>Whether there is a position worth restoring.</summary>
    public bool IsMeaningful => Index > 0 || Delta < 0;
}
