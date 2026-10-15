using Microsoft.UI.Xaml.Controls;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

/// <summary>The Downloads page: the curated catalogue of apps, each installed through winget.</summary>
public sealed partial class DownloadsPage : Page
{
    public DownloadsViewModel ViewModel { get; }

    public DownloadsPage(DownloadsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }
}
