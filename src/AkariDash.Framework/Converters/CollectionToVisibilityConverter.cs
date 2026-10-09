using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AkariDash.Framework.Converters;

/// <summary>Shows the target when the bound collection has at least one item.</summary>
public sealed class CollectionToVisibilityConverter : IValueConverter
{
    /// <summary>When true, an empty collection produces Visible instead of Collapsed.</summary>
    public bool Invert { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        var isEmpty = value is not System.Collections.IEnumerable items || !items.Cast<object?>().Any();
        return isEmpty == Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}
