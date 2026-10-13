using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using AkariDash.App.Services;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

/// <summary>The Uninstaller page: Store and Win32 apps, ticked and removed.</summary>
public sealed partial class UninstallerPage : Page
{
    /// <summary>Localized string accessor used by x:Bind function bindings.</summary>
    public LocalizedStrings Strings { get; }

    public UninstallerViewModel ViewModel { get; }

    public UninstallerPage(UninstallerViewModel viewModel)
    {
        Strings = App.Services.GetRequiredService<LocalizedStrings>();
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }
}
