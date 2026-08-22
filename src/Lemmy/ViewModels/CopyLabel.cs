using System.Globalization;
using Avalonia.Data.Converters;

namespace Lemmy.ViewModels;

/// <summary>
/// Turns "has this been copied" into what the button says. The clipboard gives no feedback of its
/// own on a phone — nothing flashes, nothing appears — so the button has to say it worked.
/// </summary>
public sealed class CopyLabel : IValueConverter
{
    /// <summary>The shared instance, for binding from XAML.</summary>
    public static CopyLabel Instance { get; } = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Copied" : "Copy";

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("The label is never read back.");
}
