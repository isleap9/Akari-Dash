using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AkariDash.Framework.Converters;

/// <summary>
/// Maps an enum value to a bool. Useful for two-way binding a set of radio buttons
/// to an enum property. Pass the target enum value as the binding parameter.
/// </summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (parameter is not string parameterString)
        {
            return false;
        }

        if (value is null || !Enum.IsDefined(value.GetType(), value))
        {
            return false;
        }

        var parameterValue = Enum.Parse(value.GetType(), parameterString);
        return parameterValue.Equals(value);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
    {
        // A radio button being unchecked in a group must not write back to the
        // source: returning null would be coerced to the enum's default value,
        // overwriting the value the user just selected via the checked radio.
        if (parameter is not string parameterString || value is not true)
        {
            return DependencyProperty.UnsetValue;
        }

        try
        {
            return Enum.Parse(targetType, parameterString);
        }
        catch (ArgumentException)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
