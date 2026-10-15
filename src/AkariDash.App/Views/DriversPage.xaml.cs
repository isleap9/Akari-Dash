using Microsoft.UI.Xaml.Controls;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

/// <summary>The Drivers page: AkariOS-Ultimate's Graphics category, entry for entry.</summary>
public sealed partial class DriversPage : Page
{
    public DriverPipelineViewModel ViewModel { get; }

    public DriversPage(DriverPipelineViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }
}
