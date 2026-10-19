using Microsoft.UI.Xaml.Controls;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

/// <summary>The GPUs page: tabs like Customize and UWP Apps, the first one the graphics card.</summary>
public sealed partial class GpusPage : Page
{
    public GpusViewModel ViewModel { get; }

    public GpusPage(GpusViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }
}
