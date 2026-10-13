using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AkariDash.Core.Apps;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>A tickable row of the Drivers or Devices tab, under its class heading.</summary>
public sealed partial class CleanupRowViewModel : ObservableObject
{
    private readonly Action _selectionChanged;

    public CleanupRowViewModel(string title, string detail, string className, string provider, string version, string date, object model, Action selectionChanged)
    {
        Title = title;
        Detail = detail;
        ClassName = className;
        Provider = provider;
        Version = version;
        Date = date;
        Model = model;
        _selectionChanged = selectionChanged;
    }

    public string Title { get; }

    public string Detail { get; }

    public string ClassName { get; }

    public string Provider { get; }

    public string Version { get; }

    public string Date { get; }

    public object Model { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => _selectionChanged();
}

/// <summary>A class heading and the rows under it, as Vain Toolbox groups drivers and devices.</summary>
public sealed class CleanupGroup(string name, IEnumerable<CleanupRowViewModel> rows) : List<CleanupRowViewModel>(rows)
{
    public string Name { get; } = name;
}

/// <summary>What the Drivers and Devices tabs share: a list grouped by class, search, select all, and a count.</summary>
public abstract partial class CleanupListViewModel : ViewModelBase
{
    private readonly List<CleanupRowViewModel> _all = [];
    private bool _loaded;

    protected CleanupListViewModel(IInfoBarService infoBar, IStatusService status, ILogger logger)
    {
        InfoBar = infoBar;
        Status = status;
        Logger = logger;
    }

    protected IInfoBarService InfoBar { get; }

    protected IStatusService Status { get; }

    protected ILogger Logger { get; }

    public ObservableCollection<CleanupGroup> Groups { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int SelectedCount { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    public bool HasSelection => SelectedCount > 0 && !IsBusy;

    partial void OnSelectedCountChanged(int value) => OnPropertyChanged(nameof(HasSelection));

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(HasSelection));

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    protected IEnumerable<CleanupRowViewModel> Selected => _all.Where(row => row.IsSelected);

    protected IEnumerable<CleanupRowViewModel> All => _all;

    protected abstract string Listing { get; }

    protected abstract string Noun { get; }

    protected abstract Task<IEnumerable<CleanupRowViewModel>> ListAsync(Action selectionChanged);

    /// <summary>Lists the first time the tab is opened, which takes a few seconds.</summary>
    public Task EnsureLoadedAsync() => _loaded ? Task.CompletedTask : LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        StatusText = $"{Listing}...";
        try
        {
            using (Status.Begin(Listing))
            {
                var rows = await ListAsync(() => SelectedCount = _all.Count(row => row.IsSelected));
                _all.Clear();
                _all.AddRange(rows);
            }

            _loaded = true;
            SelectedCount = 0;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Listing} failed", Listing);
            InfoBar.ShowError(Title, $"{Listing} failed: {ex.Message}");
            StatusText = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        var select = Groups.SelectMany(group => group).Any(row => !row.IsSelected);
        foreach (var row in Groups.SelectMany(group => group))
        {
            row.IsSelected = select;
        }
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var matches = _all.Where(row => query.Length == 0 ||
            row.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            row.Detail.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            row.ClassName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            row.Provider.Contains(query, StringComparison.CurrentCultureIgnoreCase));

        Groups.Clear();
        foreach (var group in matches.GroupBy(row => row.ClassName, StringComparer.OrdinalIgnoreCase))
        {
            Groups.Add(new CleanupGroup(group.Key, group));
        }

        StatusText = $"Found {_all.Count} {Noun}.";
    }

    /// <summary>Runs one step per selected row, reports failures together, and lists again.</summary>
    protected async Task RunOnSelectedAsync(IReadOnlyList<CleanupRowViewModel> rows, string doing, Func<CleanupRowViewModel, Task> step, string done)
    {
        IsBusy = true;
        var failed = new List<string>();
        try
        {
            foreach (var row in rows)
            {
                try
                {
                    using (Status.Begin($"{doing} {row.Title}"))
                    {
                        await step(row);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "{Doing} {Row} failed", doing, row.Title);
                    failed.Add(ex.Message);
                }
            }
        }
        finally
        {
            IsBusy = false;
        }

        if (failed.Count == 0)
        {
            InfoBar.ShowSuccess(Title, done);
        }
        else
        {
            var report = new StringBuilder($"{rows.Count - failed.Count} done, {failed.Count} failed:");
            foreach (var reason in failed)
            {
                report.AppendLine().Append($"    {reason}");
            }

            InfoBar.ShowWarning(Title, report.ToString());
        }
    }
}

/// <summary>The Drivers tab: third-party driver packages in the driver store, to export or delete.</summary>
public sealed partial class DriversViewModel : CleanupListViewModel
{
    private readonly ISystemCleanupService _cleanup;
    private readonly IDialogService _dialogs;

    public DriversViewModel(ISystemCleanupService cleanup, IDialogService dialogs, IInfoBarService infoBar, IStatusService status, ILogger<DriversViewModel> logger)
        : base(infoBar, status, logger)
    {
        _cleanup = cleanup;
        _dialogs = dialogs;
        Title = "Drivers";
    }

    protected override string Listing => "Listing drivers";

    protected override string Noun => "drivers";

    protected override async Task<IEnumerable<CleanupRowViewModel>> ListAsync(Action selectionChanged) =>
        (await _cleanup.ListDriversAsync()).Select(driver => new CleanupRowViewModel(
            driver.DisplayName,
            $"{driver.PublishedName} · {driver.OriginalName}",
            driver.ClassName,
            driver.Provider,
            driver.Version,
            driver.Date?.ToString("d") ?? string.Empty,
            driver,
            selectionChanged));

    /// <summary>Exports to Documents\Akari-Dash\Drivers\&lt;now&gt;: file pickers do not open in an elevated app.</summary>
    [RelayCommand]
    private async Task ExportSelectedAsync()
    {
        var rows = Selected.ToList();
        if (rows.Count == 0 || IsBusy)
        {
            return;
        }

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Akari-Dash", "Drivers", DateTime.Now.ToString("yyyy-MM-dd HH-mm"));
        await RunOnSelectedAsync(rows, "Exporting", row => _cleanup.ExportDriverAsync((DriverPackage)row.Model, folder),
            $"Exported {rows.Count} driver{(rows.Count == 1 ? string.Empty : "s")} to {folder}.");

        if (Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var rows = Selected.ToList();
        if (rows.Count == 0 || IsBusy)
        {
            return;
        }

        var drivers = rows.Select(row => (DriverPackage)row.Model).ToList();
        var names = string.Join(Environment.NewLine, rows.Select(row => $"    {row.Title} ({((DriverPackage)row.Model).PublishedName})"));
        var critical = drivers.Any(driver => driver.IsCritical)
            ? $"{Environment.NewLine}{Environment.NewLine}Some of these drive the graphics card, the network, storage or USB. A device left without its driver stops working until Windows finds another one; export them first if you are not sure."
            : string.Empty;

        var confirmed = await _dialogs.ConfirmAsync(
            $"Delete {rows.Count} driver{(rows.Count == 1 ? string.Empty : "s")}?",
            $"Each is uninstalled from the devices that use it and removed from the driver store. This cannot be undone, except by installing the driver again.{critical}{Environment.NewLine}{Environment.NewLine}{names}",
            "Delete",
            "Cancel");

        if (confirmed)
        {
            await RunOnSelectedAsync(rows, "Deleting", row => _cleanup.DeleteDriverAsync((DriverPackage)row.Model),
                $"Deleted {rows.Count} driver{(rows.Count == 1 ? string.Empty : "s")}.");
            await LoadAsync();
        }
    }
}

/// <summary>The Devices tab: devices Windows remembers that are not connected, to clean up.</summary>
public sealed partial class DevicesViewModel : CleanupListViewModel
{
    private readonly ISystemCleanupService _cleanup;
    private readonly IDialogService _dialogs;

    public DevicesViewModel(ISystemCleanupService cleanup, IDialogService dialogs, IInfoBarService infoBar, IStatusService status, ILogger<DevicesViewModel> logger)
        : base(infoBar, status, logger)
    {
        _cleanup = cleanup;
        _dialogs = dialogs;
        Title = "Devices";
    }

    protected override string Listing => "Looking for disconnected devices";

    protected override string Noun => "disconnected devices";

    protected override async Task<IEnumerable<CleanupRowViewModel>> ListAsync(Action selectionChanged) =>
        (await _cleanup.ListGhostDevicesAsync()).Select(device => new CleanupRowViewModel(
            device.Name, device.InstanceId, device.ClassName, string.Empty, string.Empty, string.Empty, device, selectionChanged));

    [RelayCommand]
    private Task CleanupSelectedAsync() => CleanupAsync(Selected.ToList());

    [RelayCommand]
    private Task CleanupAllAsync() => CleanupAsync(All.ToList());

    private async Task CleanupAsync(IReadOnlyList<CleanupRowViewModel> rows)
    {
        if (rows.Count == 0 || IsBusy)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            $"Remove {rows.Count} disconnected device{(rows.Count == 1 ? string.Empty : "s")}?",
            "Windows forgets these devices and their settings. Plug one back in and Windows sets it up again as new; its drivers stay on this PC.",
            "Remove",
            "Cancel");

        if (confirmed)
        {
            await RunOnSelectedAsync(rows, "Removing", row => _cleanup.RemoveDeviceAsync((GhostDevice)row.Model),
                $"Removed {rows.Count} device{(rows.Count == 1 ? string.Empty : "s")}.");
            await LoadAsync();
        }
    }
}

/// <summary>A row of the Drives tab's results.</summary>
public sealed record DiskEntryRow(string Name, string Path, string Size, string Files, double Share, string Glyph);

/// <summary>
/// The Drives tab, after Vain Toolbox's: pick a drive and scan it to see what fills it, its biggest
/// folders and biggest files. Read-only.
/// </summary>
public sealed partial class DrivesViewModel : ViewModelBase
{
    private readonly IStatusService _status;
    private readonly IInfoBarService _infoBar;
    private CancellationTokenSource? _scan;

    public DrivesViewModel(IStatusService status, IInfoBarService infoBar)
    {
        _status = status;
        _infoBar = infoBar;
        Title = "Drives";
        Drives = [.. DriveInfo.GetDrives().Where(drive => drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable).Select(drive => drive.Name)];
        SelectedDrive = Drives.FirstOrDefault(drive => drive.StartsWith(Path.GetPathRoot(Environment.SystemDirectory)!, StringComparison.OrdinalIgnoreCase)) ?? Drives.FirstOrDefault();
    }

    public IReadOnlyList<string> Drives { get; }

    [ObservableProperty]
    public partial string? SelectedDrive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsScanning { get; set; }

    public bool IsIdle => !IsScanning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult), nameof(IsEmpty))]
    public partial DiskScan? Result { get; set; }

    public bool HasResult => Result is not null;

    public bool IsEmpty => Result is null;

    [ObservableProperty]
    public partial string ScannedText { get; set; } = "—";

    [ObservableProperty]
    public partial string FilesText { get; set; } = "—";

    [ObservableProperty]
    public partial string TimeText { get; set; } = "—";

    public ObservableCollection<DiskEntryRow> Folders { get; } = [];

    public ObservableCollection<DiskEntryRow> LargestFiles { get; } = [];

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning)
        {
            _scan?.Cancel();
            return;
        }

        if (SelectedDrive is not { } drive)
        {
            return;
        }

        IsScanning = true;
        _scan = new CancellationTokenSource();
        var progress = new Progress<long>(files => FilesText = $"{files:N0}...");
        try
        {
            DiskScan scan;
            using (_status.Begin($"Scanning {drive}"))
            {
                var token = _scan.Token;
                scan = await Task.Run(() => DiskUsage.Scan(drive, progress, token), token);
            }

            Result = scan;
            ScannedText = DiskUsage.Format(scan.Bytes);
            FilesText = scan.Files.ToString("N0");
            TimeText = $"{scan.Elapsed.TotalSeconds:0.0} s";

            Folders.Clear();
            foreach (var folder in scan.TopFolders.Take(30))
            {
                Folders.Add(new DiskEntryRow(folder.Name, folder.Path, DiskUsage.Format(folder.Bytes), $"{folder.Files:N0} files",
                    scan.Bytes == 0 ? 0 : 100.0 * folder.Bytes / scan.Bytes, ""));
            }

            LargestFiles.Clear();
            foreach (var file in scan.LargestFiles)
            {
                LargestFiles.Add(new DiskEntryRow(file.Name, file.Path, DiskUsage.Format(file.Bytes), string.Empty,
                    scan.Bytes == 0 ? 0 : 100.0 * file.Bytes / scan.Bytes, ""));
            }
        }
        catch (OperationCanceledException)
        {
            FilesText = "—";
        }
        catch (Exception ex)
        {
            _infoBar.ShowError("Drives", $"Could not scan {drive}: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
            _scan.Dispose();
            _scan = null;
        }
    }

    /// <summary>Opens a result in File Explorer, selected in its folder.</summary>
    [RelayCommand]
    private static void Reveal(DiskEntryRow? row)
    {
        if (row is not null)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{row.Path}\"") { UseShellExecute = true });
        }
    }
}
