using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using AkariDash.App.Services;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

public sealed partial class HomePage : Page
{
    /// <summary>Localized string accessor used by x:Bind function bindings.</summary>
    public LocalizedStrings Strings { get; }

    public HomePage(HomeViewModel viewModel)
    {
        Strings = App.Services.GetRequiredService<LocalizedStrings>();
        InitializeComponent();
        DataContext = viewModel;
    }
}
