using Microsoft.UI.Xaml.Data;

namespace AkariDash.Framework.Converters;

/// <summary>
/// Evaluates basic arithmetic on numeric values or strings.
/// The binding parameter specifies the operator: +, -, *, /, %, or = (equality).
/// When two values are passed via a multi-binding array the first two are used.
/// </summary>
public sealed class MathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is not object[] values || values.Length < 2)
        {
            return null;
        }

        var a = values[0];
        var b = values[1];
        return Compute(a, b, parameter as string);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();

    private static object? Compute(object? a, object? b, string? op)
    {
        if (a is null || b is null)
        {
            return null;
        }

        return op switch
        {
            "+" => Add(a, b),
            "-" => System.Convert.ToDouble(a) - System.Convert.ToDouble(b),
            "*" => System.Convert.ToDouble(a) * System.Convert.ToDouble(b),
            "/" => b is 0 or 0d ? null : System.Convert.ToDouble(a) / System.Convert.ToDouble(b),
            "%" => System.Convert.ToDouble(a) % System.Convert.ToDouble(b),
            "=" => Equals(a, b),
            "!=" => !Equals(a, b),
            _ => null,
        };
    }

    private static object Add(object a, object b)
    {
        if (a is string || b is string)
        {
            return string.Concat(a, b);
        }

        return System.Convert.ToDouble(a) + System.Convert.ToDouble(b);
    }
}
