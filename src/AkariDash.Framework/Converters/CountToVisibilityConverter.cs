using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AkariDash.Framework.Converters;

/// <summary>
/// Converts an integer count to <see cref="Visibility"/>.
/// Use the binding parameter to set a minimum count that must be reached (default 1).
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        var count = value is int i ? i : 0;
        var minimum = int.TryParse(parameter as string, out var parsed) ? parsed : 1;
        return count >= minimum ? Visibility.Visible : Visibility.Collapsed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}
