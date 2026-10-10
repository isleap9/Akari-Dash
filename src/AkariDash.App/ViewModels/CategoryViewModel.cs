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
    private string? _pendingTitle;
    private string? _pendingMessage;
    private bool _busy;

    /// <summary>The Category this page shows; set by the page when it is created.</summary>
    public Category Category { get; set; }

    public IReadOnlyList<TweakGroupViewModel> Groups
    {
        get => _groups;
        private set => SetProperty(ref _groups, value);
    }

    /// <summary>The pending banner's title, or <see langword="null"/> when nothing waits on a sign-out or restart.</summary>
    public string? PendingTitle
    {
        get => _pendingTitle;
        private set
        {
            if (SetProperty(ref _pendingTitle, value))
            {
                OnPropertyChanged(nameof(HasPending));
            }
        }
    }

    /// <summary>Which Tweaks wait on a restart and which on a sign-out.</summary>
    public string? PendingMessage
    {
        get => _pendingMessage;
        private set => SetProperty(ref _pendingMessage, value);
    }

    public bool HasPending => PendingTitle is not null;

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

        ShowPending(engine.Pending);
    }

    /// <summary>Summarises every Tweak changed this session that still waits on a sign-out or restart, across all Categories.</summary>
    private void ShowPending(PendingActivation pending)
    {
        if (pending.IsEmpty)
        {
            PendingTitle = null;
            PendingMessage = null;
            return;
        }

        PendingTitle = pending.NeedsRestart ? "Restart to finish" : "Sign out to finish";

        var lines = new List<string>();
        AddWaiting(lines, "a restart", pending.AfterRestart);
        AddWaiting(lines, "a sign-out", pending.AfterSignOut);

        if (BuildInfo.IsDryRunOnly)
        {
            lines.Add("Dry Run only: nothing was written to this PC, so nothing is actually waiting.");
        }

        PendingMessage = string.Join(" ", lines);
    }

    private static void AddWaiting(List<string> lines, string waitingFor, IReadOnlyList<DeclaredTweak> tweaks)
    {
        if (tweaks.Count > 0)
        {
            lines.Add($"Waiting for {waitingFor}: {string.Join(", ", tweaks.Select(tweak => tweak.Title))}.");
        }
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
                BuildInfo.PreviewNote,
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
