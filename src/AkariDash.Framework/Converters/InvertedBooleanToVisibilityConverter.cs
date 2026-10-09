using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AkariDash.Framework.Converters;

/// <summary>Converts a bool to <see cref="Visibility"/> (true = Collapsed, false = Visible).</summary>
public sealed class InvertedBooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => value is not Visibility.Visible;
}
