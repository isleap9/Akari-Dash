using Microsoft.UI.Xaml.Controls;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

/// <summary>The Drivers page: install, configure and debloat the driver for each graphics card in the PC.</summary>
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
