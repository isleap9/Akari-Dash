using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using AkariDash.App.Services;

namespace AkariDash.App.Views;

/// <summary>The Gaming Category page.</summary>
public sealed partial class GamingPage : Page
{
    /// <summary>Localized string accessor used by x:Bind function bindings.</summary>
    public LocalizedStrings Strings { get; }

    public GamingPage()
    {
        Strings = App.Services.GetRequiredService<LocalizedStrings>();
        InitializeComponent();
    }
}
