using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AkariDash.Framework.Converters;

/// <summary>Collapses the target when the bound value is null.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <summary>When true, a null value produces Visible instead of Collapsed.</summary>
    public bool Invert { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        var isNull = value is null;
        return isNull == Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}
