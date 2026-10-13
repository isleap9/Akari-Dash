using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariDash.App.Services;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

/// <summary>The UWP Apps page: Store apps, Restoration, Features, Uninstaller, Drivers, Devices and Drives, one tab each.</summary>
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

    private void OnLargestFileClick(object sender, ItemClickEventArgs e) => ViewModel.Drives.RevealCommand.Execute(e.ClickedItem as DiskEntryRow);

    private void OnTabChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var index = sender.Items.IndexOf(sender.SelectedItem);
        StoreAppsTab.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        RestorationTab.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        FeaturesTab.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        UninstallerTab.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        DriversTab.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
        DevicesTab.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;
        DrivesTab.Visibility = index == 6 ? Visibility.Visible : Visibility.Collapsed;

        // Features, drivers and devices take a few seconds to list, so each tab asks only when first opened.
        switch (index)
        {
            case 2:
                _ = ViewModel.Features.EnsureLoadedAsync();
                break;
            case 4:
                _ = ViewModel.Drivers.EnsureLoadedAsync();
                break;
            case 5:
                _ = ViewModel.Devices.EnsureLoadedAsync();
                break;
        }
    }
}
