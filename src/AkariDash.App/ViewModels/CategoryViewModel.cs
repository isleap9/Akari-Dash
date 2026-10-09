using AkariDash.Core.Tweaks;
using AkariDash.Framework.Navigation;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>Lists a Category's Groups and Tweaks, re-reading every Live State each time the page is opened.</summary>
public sealed class CategoryViewModel(TweakEngine engine, IDialogService dialogs, IInfoBarService infoBar)
    : ViewModelBase, INavigationAware
{
    private IReadOnlyList<TweakGroupViewModel> _groups = [];
    private bool _choosing;

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
                group.Select(tweak => new TweakViewModel(tweak, engine.ReadLiveState(tweak), ChooseOption)).ToList()))
            .ToList();
    }

    // async void: called from the toggle's binding setter, and never throws (errors go to the info bar).
    private async void ChooseOption(DeclaredTweak tweak, TweakOption option)
    {
        // Ignore flips while a preview is open; the Refresh after it resets every toggle.
        if (_choosing)
        {
            return;
        }

        _choosing = true;
        try
        {
            // Leave the binding setter before rebuilding the rows that hold the toggle.
            await Task.Yield();

            var changes = engine.Preview(tweak, option);
            if (changes.Count == 0)
            {
                return;
            }

            var confirmed = await dialogs.ConfirmAsync(
                $"{tweak.Title}: {option.Label}",
                "What will change:" + Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, changes) + Environment.NewLine + Environment.NewLine +
                    "Dry Run only: nothing will be written to this PC.",
                "Apply",
                "Cancel");

            if (confirmed)
            {
                engine.Apply(tweak, option);
                infoBar.ShowSuccess(
                    $"{tweak.Title}: {option.Label}",
                    $"Dry Run recorded {changes.Count} change(s); nothing was written to this PC, so its Live State is unchanged.");
            }
        }
        catch (Exception ex)
        {
            infoBar.ShowError(tweak.Title, ex.Message);
        }
        finally
        {
            // Re-read so the toggle shows the Live State, not the click.
            _choosing = false;
            Refresh();
        }
    }
}
