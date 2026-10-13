using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

/// <summary>The Customize page: Start menu, Taskbar and File Explorer, one tab each.</summary>
public sealed partial class CustomizePage : Page
{
    public CustomizeViewModel ViewModel { get; }

    public CustomizePage(CustomizeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;

        // The view model picks the tab when it is navigated to, which happens after this page is built.
        Loaded += (_, _) => Tabs.SelectedItem = Tabs.Items[ViewModel.TabIndex];
    }

    private void OnTabChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var index = sender.Items.IndexOf(sender.SelectedItem);
        StartMenuTab.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        TaskbarTab.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        ExplorerTab.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
    }
}
