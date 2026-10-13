using AkariDash.Framework.Navigation;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>
/// The UWP Apps page: Store apps, Restoration and the Uninstaller as tabs. Each tab keeps its own view
/// model; this one loads them when the page opens and tells each when another changed the PC.
/// </summary>
public sealed class UwpAppsViewModel : ViewModelBase, INavigationAware
{
    public UwpAppsViewModel(StoreAppsViewModel storeApps, RestorationViewModel restoration, UninstallerViewModel uninstaller)
    {
        StoreApps = storeApps;
        Restoration = restoration;
        Uninstaller = uninstaller;
        Title = "UWP Apps";

        storeApps.Changed += (_, _) => _ = restoration.LoadAsync();
        uninstaller.Changed += (_, _) =>
        {
            _ = restoration.LoadAsync();
            _ = storeApps.LoadAsync();
        };
        restoration.Changed += (_, _) =>
        {
            _ = storeApps.LoadAsync();
            uninstaller.OnNavigatedTo(null);
        };
    }

    public StoreAppsViewModel StoreApps { get; }

    public RestorationViewModel Restoration { get; }

    public UninstallerViewModel Uninstaller { get; }

    public void OnNavigatedTo(object? parameter)
    {
        _ = StoreApps.LoadAsync();
        _ = Restoration.LoadAsync();
        Uninstaller.OnNavigatedTo(parameter);
    }

    public void OnNavigatedFrom() => Uninstaller.OnNavigatedFrom();
}
