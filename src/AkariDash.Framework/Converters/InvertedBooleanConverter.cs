using Microsoft.UI.Xaml.Data;

namespace AkariDash.Framework.Converters;

/// <summary>Negates a bool value.</summary>
public sealed class InvertedBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
        => value is not true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => value is not true;
}
