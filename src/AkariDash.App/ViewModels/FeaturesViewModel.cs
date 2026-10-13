using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AkariDash.Core.Apps;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>One row of the Features tab: the feature and its Off / On pair, as Vain Toolbox draws it.</summary>
public sealed partial class FeatureRowViewModel : ObservableObject
{
    public FeatureRowViewModel(WindowsFeature feature, IAsyncRelayCommand<FeatureRowViewModel?> turnOff, IAsyncRelayCommand<FeatureRowViewModel?> turnOn)
    {
        Model = feature;
        TurnOffCommand = turnOff;
        TurnOnCommand = turnOn;
        KeptBecause = WindowsFeatureKeepList.KeptBecause(feature);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOn), nameof(IsOff))]
    public partial WindowsFeature Model { get; set; }

    public IAsyncRelayCommand<FeatureRowViewModel?> TurnOffCommand { get; }

    public IAsyncRelayCommand<FeatureRowViewModel?> TurnOnCommand { get; }

    public string DisplayName => Model.DisplayName;

    public string Name => Model.Name;

    public bool IsOn => Model.IsOn;

    public bool IsOff => !Model.IsOn;

    /// <summary>Capabilities are added and removed; features are switched on and off.</summary>
    public string OffLabel => Model.Kind == WindowsFeatureKind.Capability ? "Remove" : "Disable";

    public string OnLabel => Model.Kind == WindowsFeatureKind.Capability ? "Add" : "Enable";

    public string? KeptBecause { get; }

    public bool IsKept => KeptBecause is not null;

    public string KeptText => KeptBecause is null ? string.Empty : $"Kept by AkariOS-Ultimate: {KeptBecause}";

    [ObservableProperty]
    public partial bool IsWorking { get; set; }

    /// <summary>A click on a toggle that is already on unchecks it; this puts both back to the machine's state.</summary>
    public void Reset()
    {
        OnPropertyChanged(nameof(IsOn));
        OnPropertyChanged(nameof(IsOff));
    }
}

/// <summary>
/// The Features tab, after Vain Toolbox's: Windows features and optional features (capabilities), each
/// switched on its own, and AkariOS-Ultimate's "UWP Features" and "Legacy Features" removal as Remove
/// all. Loaded the first time the tab opens, because DISM takes a few seconds to list them.
/// </summary>
public sealed partial class FeaturesViewModel : ViewModelBase
{
    public static IReadOnlyList<string> Filters { get; } = ["Windows features", "Optional features"];

    private readonly IWindowsFeatureService _features;
    private readonly IDialogService _dialogs;
    private readonly IInfoBarService _infoBar;
    private readonly IStatusService _status;
    private readonly ILogger<FeaturesViewModel> _logger;
    private readonly List<FeatureRowViewModel> _all = [];
    private bool _loaded;

    public FeaturesViewModel(
        IWindowsFeatureService features, IDialogService dialogs, IInfoBarService infoBar, IStatusService status, ILogger<FeaturesViewModel> logger)
    {
        _features = features;
        _dialogs = dialogs;
        _infoBar = infoBar;
        _status = status;
        _logger = logger;
        Title = "Features";
    }

    public ObservableCollection<FeatureRowViewModel> Filtered { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int FilterIndex { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveAllCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnFilterIndexChanged(int value) => ApplyFilter();

    /// <summary>Lists the features the first time the tab is opened.</summary>
    public Task EnsureLoadedAsync() => _loaded ? Task.CompletedTask : LoadAsync();

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusText = "Asking Windows for its features...";
        try
        {
            using (_status.Begin("Listing Windows features"))
            {
                var features = await _features.ListAsync();
                _all.Clear();
                _all.AddRange(features.Select(feature => new FeatureRowViewModel(feature, TurnOffCommand, TurnOnCommand)));
            }

            _loaded = true;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not list Windows features");
            _infoBar.ShowError("Features", $"Could not list Windows features: {ex.Message}");
            StatusText = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var kind = FilterIndex == 0 ? WindowsFeatureKind.Feature : WindowsFeatureKind.Capability;
        var query = SearchText.Trim();
        var matches = _all.Where(row => row.Model.Kind == kind &&
            (query.Length == 0 ||
             row.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
             row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)));

        Filtered.Clear();
        foreach (var row in matches)
        {
            Filtered.Add(row);
        }

        var total = _all.Count(row => row.Model.Kind == kind);
        StatusText = $"Showing {Filtered.Count} of {total}, {_all.Count(row => row.Model.Kind == kind && row.IsOn)} on.";
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task TurnOnAsync(FeatureRowViewModel? row)
    {
        if (row is null || row.IsOn || IsBusy)
        {
            row?.Reset();
            return;
        }

        await ChangeAsync([row], on: true);
    }

    [RelayCommand]
    private async Task TurnOffAsync(FeatureRowViewModel? row)
    {
        if (row is null || row.IsOff || IsBusy)
        {
            row?.Reset();
            return;
        }

        if (row.KeptBecause is { } reason &&
            !await _dialogs.ConfirmAsync($"Turn off {row.DisplayName}?", $"AkariOS-Ultimate keeps this one on: {reason}.", row.OffLabel, "Cancel"))
        {
            row.Reset();
            return;
        }

        await ChangeAsync([row], on: false);
    }

    private bool CanRemoveAll() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRemoveAll))]
    private async Task RemoveAllAsync()
    {
        if (!_loaded)
        {
            await LoadAsync();
        }

        var doomed = _all.Where(row => WindowsFeatureKeepList.RemovedByRemoveAll(row.Model)).ToList();
        if (doomed.Count == 0)
        {
            _infoBar.ShowInfo("Features", "Nothing left to remove: every feature still on is on AkariOS-Ultimate's keep-list.");
            return;
        }

        var names = string.Join(Environment.NewLine, doomed.Select(row => $"    {row.DisplayName}"));
        var confirmed = await _dialogs.ConfirmAsync(
            $"Turn off {doomed.Count} feature{(doomed.Count == 1 ? string.Empty : "s")}?",
            $"AkariOS-Ultimate's UWP Features and Legacy Features removal: every optional feature removed and every Windows feature disabled, except its keep-list (.NET, DirectPlay, Wi-Fi and Ethernet, Notepad, Paint, VBScript, WMIC, Search, Defender). This can take a long while, and Windows will want a restart.{Environment.NewLine}{Environment.NewLine}{names}",
            "Turn off all",
            "Cancel");

        if (confirmed)
        {
            await ChangeAsync(doomed, on: false);
        }
    }

    private async Task ChangeAsync(IReadOnlyList<FeatureRowViewModel> rows, bool on)
    {
        IsBusy = true;
        var failed = new List<(string Name, string Error)>();
        var restart = false;
        try
        {
            foreach (var row in rows)
            {
                row.IsWorking = true;
                try
                {
                    using (_status.Begin($"{(on ? row.OnLabel : row.OffLabel).TrimEnd('e')}ing {row.DisplayName}"))
                    {
                        _logger.LogInformation("{Change} {Feature}", on ? "Turning on" : "Turning off", row.Name);
                        restart |= on ? await _features.TurnOnAsync(row.Model) : await _features.TurnOffAsync(row.Model);
                    }

                    row.Model = row.Model with { IsOn = on };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Changing {Feature} failed", row.Name);
                    failed.Add((row.DisplayName, ex.Message));
                    row.Reset();
                }
                finally
                {
                    row.IsWorking = false;
                }
            }
        }
        finally
        {
            IsBusy = false;
            ApplyFilter();
        }

        if (failed.Count > 0)
        {
            var report = new StringBuilder($"Changed {rows.Count - failed.Count}, failed {failed.Count}:");
            foreach (var (name, error) in failed)
            {
                report.AppendLine().Append($"    {name}: {error}");
            }

            _infoBar.ShowWarning("Features", report.ToString());
        }
        else if (restart)
        {
            _infoBar.ShowWarning("Features", "Done. Restart Windows to finish.");
        }
        else
        {
            _infoBar.ShowSuccess("Features", $"Changed {rows.Count} feature{(rows.Count == 1 ? string.Empty : "s")}.");
        }
    }
}
