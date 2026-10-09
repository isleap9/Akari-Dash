using System.ComponentModel;
using System.Reflection;
using Microsoft.UI.Xaml.Data;

namespace AkariDash.Framework.Converters;

/// <summary>
/// Converts an enum to its <see cref="DescriptionAttribute"/> text, falling back to the
/// member name when no description is present.
/// </summary>
public sealed class EnumToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var type = value.GetType();
        if (!type.IsEnum)
        {
            return value.ToString();
        }

        var name = value.ToString() ?? string.Empty;
        var member = type.GetMember(name).FirstOrDefault();
        var description = member?.GetCustomAttribute<DescriptionAttribute>()?.Description;
        return string.IsNullOrEmpty(description) ? name : description;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}
