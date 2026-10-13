using AkariDash.Core.Tweaks;
using AkariDash.Framework.Navigation;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>
/// The Customize page: Start menu, Taskbar and File Explorer as tabs on one page, as UWP Apps has its
/// tabs. Each tab is an ordinary Category; a search result for any of them opens this page on its tab.
/// </summary>
public sealed class CustomizeViewModel : ViewModelBase, INavigationAware
{
    public CustomizeViewModel(CategoryViewModel startMenu, CategoryViewModel taskbar, CategoryViewModel explorer)
    {
        StartMenu = startMenu;
        Taskbar = taskbar;
        Explorer = explorer;
        StartMenu.Category = Category.StartMenu;
        Taskbar.Category = Category.Taskbar;
        Explorer.Category = Category.Explorer;
        Title = "Customize";
    }

    public CategoryViewModel StartMenu { get; }

    public CategoryViewModel Taskbar { get; }

    public CategoryViewModel Explorer { get; }

    /// <summary>The tab to show: 0 Start menu, 1 Taskbar, 2 File Explorer.</summary>
    public int TabIndex { get; private set; }

    public void OnNavigatedTo(object? parameter)
    {
        // The parameter is a Tweak id from the search; it decides the tab and is passed to that tab only.
        var category = parameter is string id
            ? TweakCatalog.All.FirstOrDefault(tweak => tweak.Id == id)?.Category
            : null;

        TabIndex = category switch
        {
            Category.Taskbar => 1,
            Category.Explorer => 2,
            Category.StartMenu => 0,
            _ => TabIndex,
        };

        StartMenu.OnNavigatedTo(category == Category.StartMenu ? parameter : null);
        Taskbar.OnNavigatedTo(category == Category.Taskbar ? parameter : null);
        Explorer.OnNavigatedTo(category == Category.Explorer ? parameter : null);
    }

    public void OnNavigatedFrom()
    {
        StartMenu.OnNavigatedFrom();
        Taskbar.OnNavigatedFrom();
        Explorer.OnNavigatedFrom();
    }
}
