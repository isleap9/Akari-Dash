using Microsoft.UI.Xaml.Controls;

namespace AkariDash.App.Views;

/// <summary>GPUs when the PC has no NVIDIA, AMD or Intel card: says so. With cards, each has its own page.</summary>
public sealed partial class GpusPage : Page
{
    public GpusPage() => InitializeComponent();
}
