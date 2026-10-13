using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariDash.App.Services;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

/// <summary>The UWP Apps page: Store apps, Restoration, Features and the Uninstaller, one tab each.</summary>
public sealed partial class UwpAppsPage : Page
{
    /// <summary>Localized string accessor used by x:Bind function bindings.</summary>
    public LocalizedStrings Strings { get; }

    public UwpAppsViewModel ViewModel { get; }

    public UwpAppsPage(UwpAppsViewModel viewModel)
    {
        Strings = App.Services.GetRequiredService<LocalizedStrings>();
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnTabChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var index = sender.Items.IndexOf(sender.SelectedItem);
        StoreAppsTab.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        RestorationTab.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        FeaturesTab.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        UninstallerTab.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;

        // DISM takes a few seconds to list features, so the tab asks only when it is first opened.
        if (index == 2)
        {
            _ = ViewModel.Features.EnsureLoadedAsync();
        }
    }
}
