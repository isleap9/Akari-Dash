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
}
