using Microsoft.UI.Xaml.Data;

namespace AkariDash.Framework.Converters;

/// <summary>
/// Maps a bool to an arbitrary true/false value pair, e.g. colors, strings or thicknesses.
/// Set <see cref="TrueValue"/> / <see cref="FalseValue"/> in XAML.
/// </summary>
public sealed class BoolToValueConverter : IValueConverter
{
    public object? TrueValue { get; set; }

    public object? FalseValue { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, string language)
        => value is true ? TrueValue : FalseValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => Equals(value, TrueValue);
}
