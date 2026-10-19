using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

/// <summary>The GPUs page: tabs like Customize and UWP Apps - Main for the graphics card, Settings for its displays.</summary>
public sealed partial class GpusPage : Page
{
    public GpusViewModel ViewModel { get; }

    public GpusPage(GpusViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnTabChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var settings = sender.Items.IndexOf(sender.SelectedItem) == 1;
        MainTab.Visibility = settings ? Visibility.Collapsed : Visibility.Visible;
        MainButtons.Visibility = settings ? Visibility.Collapsed : Visibility.Visible;
        SettingsTab.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;
        SettingsButtons.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;
        if (settings)
        {
            ViewModel.OpenSettings();
        }
    }
}
