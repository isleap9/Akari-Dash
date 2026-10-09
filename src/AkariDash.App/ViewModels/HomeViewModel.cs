using CommunityToolkit.Mvvm.Input;
using AkariDash.App.Views;
using AkariDash.Framework.Navigation;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    public HomeViewModel(INavigationService navigation, IDialogService dialogs)
    {
        _navigation = navigation;
        _dialogs = dialogs;
        Title = "Home";
    }

    [RelayCommand]
    private void OpenSettings() => _navigation.NavigateTo<SettingsPage>();

    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        await _dialogs.ShowInfoAsync(
            $"About {App.AppName}",
            $"{App.AppName}\nVersion {App.AppVersion}\n\nSettings are stored in:\n{App.SettingsFilePath}");
    }
}
