using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using AkariDash.Framework.Navigation;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>
/// The Drivers page: AkariOS-Ultimate's Graphics category, entry for entry. Each row does what its
/// AkariOS entry does; one runs at a time, as in AkariOS. A choice for a card that is not in this PC
/// is greyed out rather than run.
/// </summary>
public sealed partial class DriverPipelineViewModel : ViewModelBase, INavigationAware
{
    private static readonly string StatePath = Path.Combine(App.SettingsFolder, "graphics-state.json");

    private readonly IDriverService _drivers;
    private readonly IMachine _machine;
    private readonly IDialogService _dialogs;
    private readonly IInfoBarService _infoBar;
    private readonly IStatusService _status;
    private readonly ILogger<DriverPipelineViewModel> _logger;
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);
    private IReadOnlySet<GpuVendor> _cards = new HashSet<GpuVendor>();

    public DriverPipelineViewModel(
        IDriverService drivers,
        IMachine machine,
        IDialogService dialogs,
        IInfoBarService infoBar,
        IStatusService status,
        ILogger<DriverPipelineViewModel> logger)
    {
        _drivers = drivers;
        _machine = machine;
        _dialogs = dialogs;
        _infoBar = infoBar;
        _status = status;
        _logger = logger;
        Title = "Drivers";
    }

    public IReadOnlyList<GraphicsEntryViewModel> Entries { get; private set; } = [];

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public void OnNavigatedTo(object? parameter)
    {
        try
        {
            _cards = _machine.GpuVendors();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not read the graphics cards");
        }

        var state = ReadState();
        Entries = GraphicsEntries.All
            .Select(entry => new GraphicsEntryViewModel(
                entry,
                state.TryGetValue(entry.Id, out var applied) ? applied : null,
                Unavailable(entry.Vendor),
                ToggleAsync,
                RunAsync,
                (_, option) => Unavailable(option.Vendor),
                RunOptionAsync))
            .ToList();
        OnPropertyChanged(nameof(Entries));

        ReportReset();
    }

    public void OnNavigatedFrom()
    {
    }

    private string? Unavailable(GpuVendor? vendor) =>
        vendor is { } card && !_cards.Contains(card) ? $"There is no {card.DisplayName()} graphics card in this PC." : null;

    /// <summary>After the restarts, says once how the DDU clean went, then tidies it away.</summary>
    private void ReportReset()
    {
        if (_drivers.ReadReset() is not { } reset)
        {
            return;
        }

        if (reset.Stage == DriverResetStage.Scheduled)
        {
            _infoBar.ShowInfo("Driver clean (DDU)", reset.Summary);
            return;
        }

        if (reset.Stage == DriverResetStage.Cleaned)
        {
            _infoBar.ShowSuccess("Driver clean (DDU)", reset.Summary);
        }
        else
        {
            _infoBar.ShowWarning("Driver clean (DDU)", reset.Summary);
        }

        _drivers.ForgetReset();
    }

    /// <summary>Optimize or Default on a Toggle row.</summary>
    private Task ToggleAsync(GraphicsEntryViewModel entry, bool optimize) =>
        RunOneAsync(entry, $"{entry.Name}: {(optimize ? "optimize" : "default")}", async progress =>
        {
            var steps = optimize ? GraphicsEntries.Apply(entry.Model.Id) : GraphicsEntries.Revert(entry.Model.Id);
            if (_drivers.IsDryRun)
            {
                return DryRun(entry.Name, _drivers.Describe(steps));
            }

            var allApplied = await _drivers.RunStepsAsync(steps, progress);
            entry.Applied = optimize;
            SaveState();
            return allApplied ? null : "Not every step applied — the log says which.";
        });

    /// <summary>The one button on an Action row.</summary>
    private Task RunAsync(GraphicsEntryViewModel entry) =>
        RunOneAsync(entry, $"{entry.Name}: {entry.Button.ToLowerInvariant()}", async progress =>
        {
            var steps = GraphicsEntries.Apply(entry.Model.Id);
            if (_drivers.IsDryRun)
            {
                return DryRun(entry.Name, _drivers.Describe(steps));
            }

            return await _drivers.RunStepsAsync(steps, progress) ? null : "Not every step applied — the log says which.";
        });

    /// <summary>A choice inside a Group row: DDU, or a driver install for one vendor.</summary>
    private async Task RunOptionAsync(GraphicsEntryViewModel entry, GraphicsOptionViewModel option)
    {
        var choice = option.Model;
        var vendor = choice.Vendor ?? GpuVendor.Nvidia;
        var row = new DriverRow($"{entry.Model.Id}.{vendor}".ToLowerInvariant(), vendor, choice.Kind, $"{entry.Name}: {choice.Name}", choice.Description, entry.Model.Risk);
        string? installer = null;

        // AkariOS asks for the downloaded file with a picker; an elevated app cannot open one, so the
        // file comes from the drop folder, and an empty folder is opened for it instead.
        if (choice.Kind == DriverStepKind.Debloat)
        {
            installer = _drivers.DroppedInstaller(vendor);
            if (installer is null)
            {
                _infoBar.ShowInfo(row.Title,
                    $"Put the {vendor.DisplayName()} driver you downloaded in {DriverInbox.Folder}, then press Install again. The folder is opening now.");
                OpenInbox();
                return;
            }
        }

        var confirm = choice.Confirm
            ?? (choice.Kind == DriverStepKind.Debloat ? $"Debloat and install {Path.GetFileName(installer)}?" : null);
        if (confirm is not null && !await _dialogs.ConfirmAsync(row.Title, confirm, choice.Button, "Cancel"))
        {
            return;
        }

        await RunOneAsync(entry, row.Title, async progress =>
        {
            if (_drivers.IsDryRun)
            {
                return DryRun(row.Title, _drivers.Describe(row, installer));
            }

            var outcome = choice.Kind switch
            {
                DriverStepKind.Clean or DriverStepKind.CleanManual => await _drivers.CleanAsync(choice.Kind == DriverStepKind.CleanManual, _cards, progress),
                DriverStepKind.Debloat => await _drivers.DebloatAsync(row, installer!, progress),
                _ => await _drivers.InstallAsync(row, progress),
            };

            return outcome switch
            {
                DriverStepOutcome.CompletedNeedsRestart => "Installed. Restart the PC to finish.",
                DriverStepOutcome.InstalledWithoutSettings => "Installed, but not every step after it applied — the log says which.",
                DriverStepOutcome.HandedOffToInstaller => "The installer was opened for you to finish.",
                DriverStepOutcome.CleanScheduled => "Restarting into safe mode for DDU.",
                _ => null,
            };
        });
    }

    /// <summary>
    /// Runs one row's work, one row at a time as AkariOS does, with the status line and the row's
    /// spinner; the work returns a note for the info bar, or null for a plain "done".
    /// </summary>
    private async Task RunOneAsync(GraphicsEntryViewModel entry, string title, Func<IProgress<string>, Task<string?>> work)
    {
        await _oneAtATime.WaitAsync();
        entry.IsWorking = true;
        IsBusy = true;
        StatusText = $"{title}…";
        try
        {
            _logger.LogInformation("Running {Title}", title);
            string? note;
            using (_status.Begin(title))
            {
                note = await work(new Progress<string>(text => StatusText = text));
            }

            if (note is null)
            {
                _infoBar.ShowSuccess("Drivers", $"{title}: done.");
            }
            else if (note.StartsWith("Not every", StringComparison.Ordinal) || note.StartsWith("Installed, but", StringComparison.Ordinal))
            {
                _infoBar.ShowWarning("Drivers", $"{title}: {note}");
            }
            else
            {
                _infoBar.ShowInfo("Drivers", $"{title}: {note}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Title} failed", title);
            _infoBar.ShowError("Drivers", $"{title}: {ex.Message}");
        }
        finally
        {
            entry.IsWorking = false;
            IsBusy = false;
            StatusText = string.Empty;
            _oneAtATime.Release();
        }
    }

    private static string DryRun(string title, string plan) =>
        $"This is the Dry Run build, so nothing was done. A real run would:{Environment.NewLine}{Environment.NewLine}{plan}";

    private void OpenInbox()
    {
        try
        {
            Directory.CreateDirectory(DriverInbox.Folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{DriverInbox.Folder}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open {Folder}", DriverInbox.Folder);
        }
    }

    /// <summary>What each Toggle row last did, kept as AkariOS keeps its state.json, for the dot.</summary>
    private Dictionary<string, bool> ReadState()
    {
        try
        {
            return File.Exists(StatePath)
                ? JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(StatePath)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogWarning(ex, "Could not read {Path}", StatePath);
            return [];
        }
    }

    private void SaveState()
    {
        try
        {
            var state = Entries.Where(entry => entry.Applied is not null).ToDictionary(entry => entry.Model.Id, entry => entry.Applied!.Value);
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(state));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not write {Path}", StatePath);
        }
    }
}
