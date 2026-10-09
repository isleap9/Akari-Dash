using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using AkariDash.App.Services;
using AkariDash.App.ViewModels;
using AkariDash.Core.Tweaks;

namespace AkariDash.App.Views;

/// <summary>The Gaming Category page.</summary>
public sealed partial class GamingPage : Page
{
    /// <summary>Localized string accessor used by x:Bind function bindings.</summary>
    public LocalizedStrings Strings { get; }

    public CategoryViewModel ViewModel { get; }

    public GamingPage(CategoryViewModel viewModel)
    {
        Strings = App.Services.GetRequiredService<LocalizedStrings>();
        ViewModel = viewModel;
        ViewModel.Category = Category.Gaming;
        InitializeComponent();
        DataContext = viewModel;
    }
}
