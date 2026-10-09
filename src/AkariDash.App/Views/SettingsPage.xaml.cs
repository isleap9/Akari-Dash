using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using AkariDash.App.Services;
using AkariDash.App.ViewModels;

namespace AkariDash.App.Views;

public sealed partial class SettingsPage : Page
{
    /// <summary>View model used by x:Bind bindings.</summary>
    public SettingsViewModel ViewModel { get; }

    /// <summary>Localized string accessor used by x:Bind function bindings.</summary>
    public LocalizedStrings Strings { get; }

    public string AppName => App.AppName;

    public string AppVersionText => $"Version {App.AppVersion}";

    public string SettingsFilePath => App.SettingsFilePath;

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        Strings = App.Services.GetRequiredService<LocalizedStrings>();
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
