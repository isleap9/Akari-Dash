using Microsoft.UI.Xaml.Data;

namespace AkariDash.Framework.Converters;

/// <summary>
/// Formats a <see cref="DateTimeOffset"/> / <see cref="DateTime"/> to a string.
/// Pass a format string (e.g. "d", "g", "yyyy-MM-dd") as the binding parameter.
/// </summary>
public sealed class DateToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        var format = parameter as string;
        return value switch
        {
            DateTimeOffset offset => format is null ? offset.LocalDateTime : offset.LocalDateTime.ToString(format),
            DateTime dateTime => format is null ? dateTime.ToString("g") : dateTime.ToString(format),
            _ => value?.ToString(),
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}
