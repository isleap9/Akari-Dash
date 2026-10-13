using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AkariDash.App;

/// <summary>Draws a section heading as a NavigationViewItemHeader and everything else as an item.</summary>
public sealed partial class NavItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ItemTemplate { get; set; }

    public DataTemplate? HeaderTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) =>
        item is NavigationItem { IsHeader: true } ? HeaderTemplate : ItemTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
