using AkariDash.Core.Tweaks;
using AkariDash.Framework.Navigation;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>Lists a Category's Groups and Tweaks, re-reading every Live State each time the page is opened.</summary>
public sealed class CategoryViewModel(TweakEngine engine) : ViewModelBase, INavigationAware
{
    private IReadOnlyList<TweakGroupViewModel> _groups = [];

    /// <summary>The Category this page shows; set by the page when it is created.</summary>
    public Category Category { get; set; }

    public IReadOnlyList<TweakGroupViewModel> Groups
    {
        get => _groups;
        private set => SetProperty(ref _groups, value);
    }

    public void OnNavigatedTo(object? parameter) => Refresh();

    public void OnNavigatedFrom()
    {
    }

    private void Refresh()
    {
        Groups = TweakCatalog.All
            .Where(tweak => tweak.Category == Category)
            .GroupBy(tweak => tweak.Group)
            .Select(group => new TweakGroupViewModel(
                group.Key,
                group.Select(tweak => new TweakViewModel(tweak, engine.ReadLiveState(tweak))).ToList()))
            .ToList();
    }
}
