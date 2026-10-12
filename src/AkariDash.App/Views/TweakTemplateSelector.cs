using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

/// <summary>Draws a Tweak as a row, or as a tuner card when it has a TunerLayout.</summary>
public sealed partial class TweakTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RowTemplate { get; set; }

    public DataTemplate? TunerTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) =>
        item is TweakViewModel { IsTuner: true } ? TunerTemplate : RowTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
