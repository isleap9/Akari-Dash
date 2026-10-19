using Microsoft.UI.Xaml.Controls;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

/// <summary>The Drivers page: AkariOS-Ultimate's Graphics category for the cards in this PC.</summary>
public sealed partial class DriversPage : Page
{
    public GraphicsViewModel ViewModel { get; }

    public DriversPage(GraphicsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }
}
