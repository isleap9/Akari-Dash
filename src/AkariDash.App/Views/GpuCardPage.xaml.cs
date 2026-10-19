using Microsoft.UI.Xaml.Controls;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

/// <summary>One graphics card's page under GPUs; the card is the navigation parameter.</summary>
public sealed partial class GpuCardPage : Page
{
    public GpuCardViewModel ViewModel { get; }

    public GpuCardPage(GpuCardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }
}
