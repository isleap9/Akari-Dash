using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AkariDash.Framework.Converters;

/// <summary>Shows the target when the bound value is not null.</summary>
public sealed class ObjectToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}
