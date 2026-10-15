using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AkariDash.Core.Apps;
using AkariDash.Framework.Navigation;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>One filter chip on the Downloads page: "All", or one category.</summary>
public sealed class CategoryChipViewModel(
    string label,
    InstallableAppCategory? category,
    bool isChecked,
    Action<CategoryChipViewModel> picked) : ViewModelBase
{
    private bool? _isChecked = isChecked;

    public string Label { get; } = label;

    /// <summary>The category this chip filters to, or <see langword="null"/> for "All".</summary>
    public InstallableAppCategory? Category { get; } = category;

    /// <summary>Nullable for ToggleButton; a chip is never indeterminate.</summary>
    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            // A chip cannot be unchecked by clicking it again; another chip has to be picked.
            if (value != true)
            {
                OnPropertyChanged();
                return;
            }

            if (SetProperty(ref _isChecked, true))
            {
                picked(this);
            }
        }
    }

    /// <summary>Unchecks this chip because another one was picked.</summary>
    internal void Clear()
    {
        if (_isChecked == true)
        {
            _isChecked = false;
            OnPropertyChanged(nameof(IsChecked));
        }
    }
}

/// <summary>
/// The Downloads page: a curated grid of apps, installed with winget. An Action surface
/// (ADR-0004): an install asks first, runs once, cannot be undone, and is read back from the
/// machine afterwards. Cards are clicked to pick them and one button installs the lot, through a
/// queue, because winget itself runs one at a time.
/// </summary>
public sealed partial class DownloadsViewModel : ViewModelBase, INavigationAware
{
    private readonly IAppInstallService _apps;
    private readonly IDialogService _dialogs;
    private readonly IInfoBarService _infoBar;
    private readonly ILogger<DownloadsViewModel> _logger;
    private readonly IStatusService _status;
    private readonly Queue<AppTileViewModel> _queue = [];
    private bool _loading;
    private bool _pumping;

    public DownloadsViewModel(
        IAppInstallService apps,
        IDialogService dialogs,
        IInfoBarService infoBar,
        IStatusService status,
        ILogger<DownloadsViewModel> logger)
    {
        _apps = apps;
        _dialogs = dialogs;
        _infoBar = infoBar;
        _status = status;
        _logger = logger;
        Title = "Downloads";

        Tiles = InstallableApps.All.Select(app => new AppTileViewModel(app, OnSelectionChanged)).ToList();

        // "All" first, then the categories in the order the enum declares them.
        Categories = new[] { new CategoryChipViewModel("All", null, true, Pick) }
            .Concat(Enum.GetValues<InstallableAppCategory>()
                .Select(category => new CategoryChipViewModel(category.ToString(), category, false, Pick)))
            .ToList();

        ApplyFilter();
    }

    /// <summary>Every app in the catalogue, in the catalogue's own order.</summary>
    public IReadOnlyList<AppTileViewModel> Tiles { get; }

    /// <summary>The tiles the current filter and search let through.</summary>
    public ObservableCollection<AppTileViewModel> Filtered { get; } = [];

    public IReadOnlyList<CategoryChipViewModel> Categories { get; }

    [ObservableProperty]
    public partial InstallableAppCategory? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>The status line: what is being installed, or why the page cannot tell you more.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    /// <summary>True while the queue is working through installs.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// How many cards are picked. Only tiles that are waiting for an install can be picked: a tile
    /// that turns out to be installed, or that the queue has taken, loses its pick.
    /// </summary>
    [ObservableProperty]
    public partial int SelectedCount { get; set; }

    /// <summary>The caption on the one Install button. It carries the count, so the bar needs no label.</summary>
    public string InstallSelectedLabel =>
        SelectedCount == 0 ? "Install selected" : $"Install selected ({SelectedCount})";

    /// <summary>
    /// Shown while nothing is picked. A card that is itself a button does not look like one, so the bar
    /// says what to do rather than leaving a new user to find out.
    /// </summary>
    public bool NothingPicked => SelectedCount == 0;

    partial void OnSelectedCountChanged(int value)
    {
        OnPropertyChanged(nameof(InstallSelectedLabel));
        OnPropertyChanged(nameof(NothingPicked));
        InstallSelectedCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedCategoryChanged(InstallableAppCategory? value) => ApplyFilter();

    public void OnNavigatedTo(object? parameter) => _ = LoadAsync();

    public void OnNavigatedFrom()
    {
    }

    private void OnSelectionChanged() => RefreshSelection();

    /// <summary>
    /// Clicking a card is the only way to pick one, so the count is asked of the tiles themselves
    /// rather than kept alongside them - there is no second copy to drift.
    /// </summary>
    private void RefreshSelection() => SelectedCount = Tiles.Count(tile => tile.IsSelected);

    private bool CanInstallSelected() => SelectedCount > 0;

    // Deliberately not gated on IsBusy: picking more while a run is going adds to the same queue,
    // which is what the queue is for.
    [RelayCommand(CanExecute = nameof(CanInstallSelected))]
    private async Task InstallSelectedAsync()
    {
        var picked = Tiles.Where(tile => tile.IsSelected).ToList();
        if (picked.Count == 0)
        {
            return;
        }

        var names = string.Join(Environment.NewLine, picked.Select(tile => $"    {tile.Name}"));
        var confirmed = await _dialogs.ConfirmAsync(
            picked.Count == 1 ? $"Install {picked[0].Name}?" : $"Install {picked.Count} apps?",
            $"The following will be installed with winget:{Environment.NewLine}{Environment.NewLine}{names}{Environment.NewLine}{Environment.NewLine}This cannot be undone.",
            "Install",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        // The dialog is modal, but the click that opened it is not: a second click queues its own
        // confirm behind this one, and both would otherwise install the same apps twice. Take the
        // picked tiles again, now that the answer can have changed.
        var queue = Tiles.Where(tile => tile.IsSelected && tile.IsInstallable).ToList();
        if (queue.Count == 0)
        {
            return;
        }

        if (_apps.IsDryRun)
        {
            // Phase 1 installs nothing: say what a real run would have done, and change no tile.
            var commands = string.Join(
                Environment.NewLine,
                queue.Select(tile => "    " + WingetCommand.DescribeInstall(tile.Model)));
            _infoBar.ShowInfo(
                queue.Count == 1 ? $"Install {queue[0].Name}" : $"Install {queue.Count} apps",
                $"This is the Dry Run build, so nothing was installed. A real run would have run:{Environment.NewLine}{Environment.NewLine}{commands}");
            return;
        }

        foreach (var tile in queue)
        {
            // The pick has done its job: it means "waiting to be installed", and it is now waiting.
            tile.IsSelected = false;
            _queue.Enqueue(tile);
            tile.State = AppTileState.Queued;
        }

        if (_pumping)
        {
            // The pump already running will reach them; one winget at a time is the point of the queue.
            return;
        }

        _ = RunQueueAsync();
    }

    /// <summary>Picks everything the filter and search are showing, so the button can take the lot.</summary>
    [RelayCommand]
    private void SelectAll()
    {
        foreach (var tile in Filtered.Where(tile => tile.IsInstallable))
        {
            tile.IsSelected = true;
        }

        RefreshSelection();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var tile in Tiles.Where(tile => tile.IsSelected))
        {
            tile.IsSelected = false;
        }

        RefreshSelection();
    }

    private void Pick(CategoryChipViewModel picked)
    {
        foreach (var chip in Categories.Where(chip => chip != picked))
        {
            chip.Clear();
        }

        SelectedCategory = picked.Category;
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        IEnumerable<AppTileViewModel> matches = Tiles;

        if (SelectedCategory is { } category)
        {
            matches = matches.Where(tile => tile.Model.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            matches = matches.Where(tile =>
                tile.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                tile.Publisher.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        }

        Filtered.Clear();
        foreach (var tile in matches)
        {
            Filtered.Add(tile);
        }
    }

    /// <summary>
    /// Asks winget which of the catalogue is on this machine. <paramref name="keepFinished"/> is set
    /// after an install run: an app that has just been reported installed keeps that answer, because
    /// a list taken immediately afterwards can still lag behind the install that just finished.
    /// </summary>
    private async Task LoadAsync(bool keepFinished = false)
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        StatusText = "Checking what is already installed…";
        try
        {
            var installed = await _apps.FindInstalledIdsAsync(Tiles.Select(tile => tile.Model).ToList());

            if (installed is null)
            {
                // We could not ask, so no tile is allowed to claim it is missing.
                StatusText = "Could not ask winget what is installed — is App Installer installed?";
                _logger.LogWarning("Could not ask winget what is installed");
            }
            else
            {
                StatusText = string.Empty;
            }

            foreach (var tile in Tiles)
            {
                if (keepFinished && tile.State is AppTileState.Installed or AppTileState.NeedsRestart)
                {
                    continue;
                }

                tile.State = installed is null
                    ? AppTileState.Unknown
                    : installed.Contains(tile.Model.WingetId) ? AppTileState.Installed : AppTileState.NotInstalled;

                // A pick on a tile that cannot be installed would leave the button counting an app it
                // cannot install, so the machine's answer takes the pick away.
                if (!tile.IsInstallable)
                {
                    tile.IsSelected = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not read back which apps are installed");
            _infoBar.ShowError("Downloads", $"Could not list apps: {ex.Message}");
            StatusText = string.Empty;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task RunQueueAsync()
    {
        _pumping = true;
        IsBusy = true;

        var installed = 0;
        var alreadyThere = 0;
        var restart = 0;
        var failed = new List<(string Name, string Error)>();

        try
        {
            while (_queue.Count > 0)
            {
                var tile = _queue.Dequeue();
                tile.State = AppTileState.Installing;
                StatusText = $"Installing {tile.Name}…";

                try
                {
                    _logger.LogInformation("Installing {App}", tile.Model.WingetId);

                    InstallOutcome outcome;
                    using (_status.Begin($"Installing {tile.Name}"))
                    {
                        outcome = await _apps.InstallAsync(tile.Model);
                    }

                    switch (outcome)
                    {
                        case InstallOutcome.Installed:
                            tile.State = AppTileState.Installed;
                            installed++;
                            break;
                        case InstallOutcome.AlreadyInstalled:
                            tile.State = AppTileState.Installed;
                            alreadyThere++;
                            break;
                        case InstallOutcome.InstalledNeedsRestart:
                            tile.State = AppTileState.NeedsRestart;
                            restart++;
                            break;
                    }

                    _logger.LogInformation("Install of {App} ended as {Outcome}", tile.Model.WingetId, outcome);
                }
                catch (Exception ex)
                {
                    // One app that will not install must not cancel the ones queued behind it.
                    _logger.LogError(ex, "Install of {App} failed", tile.Model.WingetId);
                    tile.State = AppTileState.Failed;
                    failed.Add((tile.Name, ex.Message));
                }
            }
        }
        finally
        {
            _pumping = false;
            IsBusy = false;
            StatusText = string.Empty;
        }

        Report(installed, alreadyThere, restart, failed);

        // Read the machine back once, leaving the tiles that just finished on winget's own answer.
        await LoadAsync(keepFinished: true);
    }

    private void Report(int installed, int alreadyThere, int restart, List<(string Name, string Error)> failed)
    {
        var done = installed + alreadyThere + restart;

        if (failed.Count == 0)
        {
            var summary = new StringBuilder($"Installed {done} app{(done == 1 ? string.Empty : "s")}.");
            if (restart > 0)
            {
                summary.Append($" {restart} of them {(restart == 1 ? "wants" : "want")} a restart to finish.");
            }

            _infoBar.ShowSuccess("Downloads", summary.ToString());
            return;
        }

        var report = new StringBuilder($"Installed {done}, failed {failed.Count}:");
        foreach (var (name, error) in failed)
        {
            report.AppendLine().Append($"    {name}: {error}");
        }

        _infoBar.ShowWarning("Downloads", report.ToString());
    }
}
