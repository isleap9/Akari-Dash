using Microsoft.UI.Xaml.Controls;

namespace AkariDash.App.Views;

/// <summary>A "Work in progress" card: the page's icon and one line on what will live there.</summary>
public sealed partial class WorkInProgressView : UserControl
{
    public WorkInProgressView() => InitializeComponent();

    public string Glyph { get; set; } = "\uE822";

    public string Description { get; set; } = string.Empty;
}
