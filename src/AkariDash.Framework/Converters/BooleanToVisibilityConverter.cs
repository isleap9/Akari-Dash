using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AkariDash.Framework.Converters;

/// <summary>Converts a bool to <see cref="Visibility"/> (true = Visible, false = Collapsed).</summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => value is Visibility.Visible;
}
