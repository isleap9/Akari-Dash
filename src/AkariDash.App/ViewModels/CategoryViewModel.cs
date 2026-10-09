using Microsoft.Extensions.Logging;
using AkariDash.Core.Tweaks;
using AkariDash.Framework.Navigation;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>Lists a Category's Groups and Tweaks, re-reading every Live State each time the page is opened.</summary>
public sealed class CategoryViewModel(
    TweakEngine engine,
    IDialogService dialogs,
    IInfoBarService infoBar,
    ILogger<CategoryViewModel> logger)
    : ViewModelBase, INavigationAware
{
    private IReadOnlyList<TweakGroupViewModel> _groups = [];
    private bool _busy;

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
                group.Select(tweak => new TweakViewModel(
                    tweak,
                    engine.ReadLiveState(tweak),
                    isApplied: engine.IsApplied(tweak),
                    ChooseOption,
                    Undo)).ToList()))
            .ToList();
    }

    // async void: called from the toggle's binding setter, and never throws (errors go to the info bar).
    private async void ChooseOption(DeclaredTweak tweak, TweakOption option) => await RunAsync(tweak, async () =>
    {
        var changes = engine.Preview(tweak, option);
        if (changes.Count == 0)
        {
            return;
        }

        var confirmed = await dialogs.ConfirmAsync(
            $"{tweak.Title}: {option.Label}",
            "What will change:" + Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, changes) +
                (BuildInfo.IsDryRunOnly
                    ? Environment.NewLine + Environment.NewLine + "Dry Run only: nothing will be written to this PC."
                    : string.Empty),
            "Apply",
            "Cancel");

        if (confirmed)
        {
            logger.LogInformation("Applying {Tweak} -> {Option}: {Changes}", tweak.Id, option.Id, string.Join("; ", changes));
            engine.Apply(tweak, option);
            logger.LogInformation("Applied {Tweak} -> {Option}", tweak.Id, option.Id);
            infoBar.ShowSuccess(
                $"{tweak.Title}: {option.Label}",
                BuildInfo.IsDryRunOnly
                    ? $"Dry Run recorded {changes.Count} change(s); nothing was written to this PC, so its Live State is unchanged."
                    : $"Applied {changes.Count} change(s).");
        }
    });

    private async void Undo(DeclaredTweak tweak) => await RunAsync(tweak, async () =>
    {
        var confirmed = await dialogs.ConfirmAsync(
            $"Undo {tweak.Title}",
            "Put back the values this PC had before Akari-Dash first changed this Tweak?",
            "Undo",
            "Cancel");

        if (confirmed)
        {
            logger.LogInformation("Undoing {Tweak}", tweak.Id);
            engine.Undo(tweak);
            logger.LogInformation("Undid {Tweak}", tweak.Id);
            infoBar.ShowSuccess(
                $"Undo {tweak.Title}",
                BuildInfo.IsDryRunOnly
                    ? "Dry Run recorded the Undo; nothing was written to this PC."
                    : "Undone: this Tweak's values are back to what this PC had before Akari-Dash changed it.");
        }
    });

    /// <summary>Runs one user action at a time, reports any failure, then re-reads every row.</summary>
    private async Task RunAsync(DeclaredTweak tweak, Func<Task> action)
    {
        // Ignore clicks while a dialog is open; the Refresh after it resets every toggle.
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            // Leave the binding setter before rebuilding the rows that hold the toggle.
            await Task.Yield();
            await action();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Tweak} failed", tweak.Id);
            infoBar.ShowError(tweak.Title, ex.Message);
        }
        finally
        {
            // Re-read so the toggle shows the Live State, not the click.
            _busy = false;
            Refresh();
        }
    }
}
