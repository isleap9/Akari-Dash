using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

/// <summary>A Category's pending banner, Groups and Tweak rows; shared by every Category.</summary>
public sealed partial class CategoryView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(CategoryViewModel), typeof(CategoryView), new PropertyMetadata(null));

    public CategoryView()
    {
        InitializeComponent();
    }

    public CategoryViewModel ViewModel
    {
        get => (CategoryViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>
    /// Scrolls the Tweak the shell's search sent the user to into view, as its card loads. Every card
    /// is asked, and only the one the search named answers, so no card has to be looked up by hand.
    /// </summary>
    private void OnTweakLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TweakViewModel { IsHighlighted: true } } card)
        {
            card.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0.3f });
        }
    }
}
