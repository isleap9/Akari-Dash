using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AkariDash.Framework.Converters;

/// <summary>
/// Shows the target when the bound string is non-empty; optionally only when it is whitespace-free.
/// </summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    /// <summary>When true, whitespace-only strings count as empty.</summary>
    public bool TrimWhitespace { get; set; } = true;

    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        var text = value as string ?? string.Empty;
        var isEmpty = TrimWhitespace ? string.IsNullOrWhiteSpace(text) : string.IsNullOrEmpty(text);
        return isEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}
