using Microsoft.UI.Xaml.Data;

namespace AkariDash.Framework.Converters;

/// <summary>
/// Passes the value through unchanged (an explicit no-op converter).
/// Useful as a placeholder or to document intent.
/// </summary>
public sealed class PassthroughConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
        => value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => value;
}
