using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using AkariDash.Framework.Navigation;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>One graphics card on the Drivers page: what is installed, what is available, and its rows.</summary>
public sealed partial class DriverSectionViewModel(GpuVendor vendor, IReadOnlyList<DriverRowViewModel> rows) : ObservableObject
{
    public GpuVendor Vendor { get; } = vendor;

    public IReadOnlyList<DriverRowViewModel> Rows { get; } = rows;

    /// <summary>The card's name as Windows has it, once read; the vendor's name until then.</summary>
    [ObservableProperty]
    public partial string Heading { get; set; } = vendor.DisplayName();

    /// <summary>The installed and the available version, in a line.</summary>
    [ObservableProperty]
    public partial string VersionLine { get; set; } = "Checking the driver…";

    [ObservableProperty]
    public partial DriverVersionState DriverState { get; set; }
}

/// <summary>
/// The Drivers page: one section per graphics card in the PC, each with its install, configure and
/// debloat rows. An Action surface (ADR-0004): every row asks first, names the card, says it cannot
/// be undone, runs through one queue - two driver installers at once is never right - and the page
/// reads the installed version back afterwards.
/// </summary>
public sealed partial class DriverPipelineViewModel : ViewModelBase, INavigationAware
{
    private readonly IDriverService _drivers;
    private readonly IMachine _machine;
    private readonly IDialogService _dialogs;
    private readonly IInfoBarService _infoBar;
    private readonly IStatusService _status;
    private readonly ILogger<DriverPipelineViewModel> _logger;
    private readonly Queue<(DriverRowViewModel Row, string? Installer)> _queue = [];
    private bool _loading;
    private bool _pumping;

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

    public IReadOnlyList<DriverSectionViewModel> Sections { get; private set; } = [];

    /// <summary>False on a PC with no PCI graphics card; the page then says so instead of showing rows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoGraphicsCard))]
    public partial bool HasGraphicsCard { get; set; } = true;

    public bool HasNoGraphicsCard => !HasGraphicsCard;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Where a person puts a driver they downloaded, for the Debloat rows.</summary>
    public string InboxFolder => DriverInbox.Folder;

    public void OnNavigatedTo(object? parameter) => _ = LoadAsync();

    public void OnNavigatedFrom()
    {
    }

    /// <summary>
    /// Builds a section per card actually in the PC, in a fixed order, then asks each vendor and the
    /// machine for the versions. Review Focus 3: no card means no sections, never every vendor's rows.
    /// </summary>
    private async Task LoadAsync()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        try
        {
            if (Sections.Count == 0)
            {
                var vendors = _machine.GpuVendors();
                Sections = Enum.GetValues<GpuVendor>()
                    .Where(vendors.Contains)
                    .Select(vendor => new DriverSectionViewModel(
                        vendor,
                        DriverRows.For(vendor).Select(row => new DriverRowViewModel(row, RunRowAsync)).ToList()))
                    .ToList();
                OnPropertyChanged(nameof(Sections));
                HasGraphicsCard = Sections.Count > 0;
                _logger.LogInformation("Drivers: found {Vendors}", string.Join(", ", Sections.Select(section => section.Vendor)));
            }

            RefreshInbox();
            await Task.WhenAll(Sections.Select(ReadVersionsAsync));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not read the graphics cards");
            _infoBar.ShowError("Drivers", $"Could not read the graphics cards: {ex.Message}");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ReadVersionsAsync(DriverSectionViewModel section)
    {
        section.VersionLine = "Checking the driver…";
        var vendor = section.Vendor;
        var installedTask = _drivers.InstalledAsync(vendor);
        var latestTask = _drivers.LatestAsync(vendor);
        var installed = await installedTask;
        var latest = await latestTask;

        if (installed is not null)
        {
            section.Heading = installed.CardName;
        }

        section.DriverState = DriverVersion.State(installed?.Version, latest);

        var have = installed?.Version is { } version ? $"{version} installed" : "could not read the installed version";
        var offered = latest is not null
            ? $"{latest.Version} available"
            : vendor == GpuVendor.Intel
                ? "Intel publishes no feed to check — see its download page"
                : "could not check the latest driver";

        section.VersionLine = section.DriverState switch
        {
            DriverVersionState.UpToDate => $"{have} — up to date",
            _ => $"{have}, {offered}",
        };
    }

    /// <summary>Lists the drop folder again, giving each Debloat row the files that are its vendor's own.</summary>
    [RelayCommand]
    private void RefreshInbox()
    {
        IReadOnlyList<string> files = [];
        try
        {
            if (Directory.Exists(DriverInbox.Folder))
            {
                files = DriverInbox.Installers(Directory.EnumerateFiles(DriverInbox.Folder));
            }
        }
        catch (Exception ex)
        {
            // A folder we cannot read is an empty one to the rows; the log says why.
            _logger.LogWarning(ex, "Could not list {Folder}", DriverInbox.Folder);
        }

        foreach (var row in Sections.SelectMany(section => section.Rows).Where(row => row.IsDebloat))
        {
            row.SetInstallers(files
                .Where(file => DriverDebloat.Plan(row.Model.Vendor, Path.GetFileName(file)) is not null)
                .Select(file => new DroppedInstaller(file)));
        }
    }

    /// <summary>Opens the drop folder in Explorer, making it first: file pickers do not open in an elevated app.</summary>
    [RelayCommand]
    private void OpenInbox()
    {
        try
        {
            Directory.CreateDirectory(DriverInbox.Folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{DriverInbox.Folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open {Folder}", DriverInbox.Folder);
            _infoBar.ShowError("Drivers", $"Could not open {DriverInbox.Folder}: {ex.Message}");
        }
    }

    private async Task RunRowAsync(DriverRowViewModel row)
    {
        if (!row.IsRunnable)
        {
            return;
        }

        var section = Sections.First(section => section.Rows.Contains(row));
        var vendor = row.Model.Vendor.DisplayName();
        string? installer = null;

        if (row.IsDebloat)
        {
            RefreshInbox();
            if (row.SelectedInstaller is not { } picked)
            {
                _infoBar.ShowInfo(
                    row.Title,
                    $"Put the {vendor} driver you downloaded in {DriverInbox.Folder}, then press Refresh. The folder is opening now.");
                OpenInbox();
                return;
            }

            installer = picked.Path;
        }

        var (title, what) = row.Model.Kind switch
        {
            DriverStepKind.Install => ($"{row.Title}?", $"The newest {vendor} driver will be downloaded and installed"),
            DriverStepKind.InstallAndConfigure => (
                $"Install and configure the {vendor} driver?",
                $"The newest {vendor} driver will be downloaded and installed, and the {vendor} settings put on with it"),
            _ => (
                $"Debloat and install {Path.GetFileName(installer)}?",
                $"{Path.GetFileName(installer)} will be unpacked, its extras removed, and what is left installed and configured"),
        };

        var handOff = row.Model.Kind != DriverStepKind.Debloat && !DriverInstallCommand.IsUnattended(row.Model.Vendor)
            ? $"{Environment.NewLine}{Environment.NewLine}{vendor} cannot be installed unattended, so {vendor}'s own installer or download page opens for you to finish."
            : string.Empty;

        var confirmed = await _dialogs.ConfirmAsync(
            title,
            $"{what} on {section.Heading}.{handOff}{Environment.NewLine}{Environment.NewLine}The screen may go dark for a moment while the driver changes. This cannot be undone.",
            "Install",
            "Cancel");

        // The dialog is modal, but the click that opened it is not: a second click queues its own
        // confirm behind this one, and both would otherwise run the row twice.
        if (!confirmed || !row.IsRunnable)
        {
            return;
        }

        if (_drivers.IsDryRun)
        {
            // Phase 1 runs nothing: say what a real run would have done, and leave the row alone.
            _infoBar.ShowInfo(
                row.Title,
                $"This is the Dry Run build, so nothing was done. A real run would:{Environment.NewLine}{Environment.NewLine}{_drivers.Describe(row.Model, installer)}");
            return;
        }

        _queue.Enqueue((row, installer));
        row.State = DriverRowState.Queued;

        if (!_pumping)
        {
            _ = RunQueueAsync();
        }
    }

    private async Task RunQueueAsync()
    {
        _pumping = true;
        IsBusy = true;
        var finished = new List<(string Title, DriverRowState State)>();
        var failed = new List<(string Title, string Error)>();

        try
        {
            while (_queue.Count > 0)
            {
                var (row, installer) = _queue.Dequeue();
                row.State = DriverRowState.Working;
                StatusText = $"{row.Title}…";
                var progress = new Progress<string>(text => StatusText = text);

                try
                {
                    _logger.LogInformation("Running {Row}", row.Model.Id);

                    DriverStepOutcome outcome;
                    using (_status.Begin(row.Title))
                    {
                        outcome = row.IsDebloat
                            ? await _drivers.DebloatAsync(row.Model, installer!, progress)
                            : await _drivers.InstallAsync(row.Model, progress);
                    }

                    row.State = outcome switch
                    {
                        DriverStepOutcome.CompletedNeedsRestart => DriverRowState.NeedsRestart,
                        DriverStepOutcome.InstalledWithoutSettings => DriverRowState.DoneWithoutSettings,
                        DriverStepOutcome.HandedOffToInstaller => DriverRowState.HandedOff,
                        _ => DriverRowState.Done,
                    };
                    finished.Add((row.Title, row.State));
                    _logger.LogInformation("{Row} ended as {Outcome}", row.Model.Id, outcome);
                }
                catch (Exception ex)
                {
                    // One row that fails must not cancel the ones queued behind it.
                    _logger.LogError(ex, "{Row} failed", row.Model.Id);
                    row.State = DriverRowState.Failed;
                    failed.Add((row.Title, ex.Message));
                }
            }
        }
        finally
        {
            _pumping = false;
            IsBusy = false;
            StatusText = string.Empty;
        }

        Report(finished, failed);

        // Read the machine back: the heading says what is installed now, not what we hoped.
        RefreshInbox();
        await Task.WhenAll(Sections.Select(ReadVersionsAsync));
    }

    private void Report(List<(string Title, DriverRowState State)> finished, List<(string Title, string Error)> failed)
    {
        var report = new StringBuilder();
        foreach (var (title, state) in finished)
        {
            report.AppendLine(state switch
            {
                DriverRowState.NeedsRestart => $"{title}: installed. Restart the PC to finish.",
                DriverRowState.DoneWithoutSettings => $"{title}: installed, but not every setting was applied — the log says which.",
                DriverRowState.HandedOff => $"{title}: the vendor's installer or page was opened for you to finish.",
                _ => $"{title}: done.",
            });
        }

        foreach (var (title, error) in failed)
        {
            report.AppendLine($"{title}: {error}");
        }

        var text = report.ToString().TrimEnd();
        if (failed.Count > 0 || finished.Any(item => item.State == DriverRowState.DoneWithoutSettings))
        {
            _infoBar.ShowWarning("Drivers", text);
        }
        else
        {
            _infoBar.ShowSuccess("Drivers", text);
        }
    }
}
