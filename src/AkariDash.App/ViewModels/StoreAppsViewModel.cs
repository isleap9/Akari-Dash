using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using AkariDash.Core.Apps;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>One row of the Store apps table.</summary>
public sealed partial class StoreAppRowViewModel : ObservableObject
{
    public StoreAppRowViewModel(StoreApp app, IAsyncRelayCommand<StoreAppRowViewModel?> remove)
    {
        Model = app;
        RemoveCommand = remove;
        ProtectedReason = AppxKeepList.ProtectedBecause(app.Name);
    }

    public StoreApp Model { get; }

    public IAsyncRelayCommand<StoreAppRowViewModel?> RemoveCommand { get; }

    public string DisplayName => Model.DisplayName;

    public string PackageName => Model.Name;

    public string Publisher => Model.Publisher;

    public string Version => Model.Version;

    public string Architecture => Model.Architecture;

    /// <summary>Why the keep-list protects it, shown in place of the remove button.</summary>
    public string? ProtectedReason { get; }

    public bool IsRemovable => ProtectedReason is null;

    public bool IsProtected => ProtectedReason is not null;

    public string ProtectedText => ProtectedReason is null ? string.Empty : $"Kept: {ProtectedReason}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIconMissing))]
    public partial ImageSource? Icon { get; set; }

    public bool IsIconMissing => Icon is null;

    /// <summary>A Store package's logo is an image file already; any failure leaves the glyph.</summary>
    public void LoadIcon()
    {
        if (Icon is not null || Model.IconPath is not { } path || !File.Exists(path))
        {
            return;
        }

        try
        {
            Icon = new BitmapImage(new Uri(path)) { DecodePixelWidth = 32 };
        }
        catch
        {
            // Leave the glyph.
        }
    }
}

/// <summary>
/// The Store apps tab: every package, filtered as Vain Toolbox filters them, each removable on its own,
/// and AkariOS-Ultimate's Remove all bloatware. Every removal keeps the app from coming back.
/// </summary>
public sealed partial class StoreAppsViewModel : ViewModelBase
{
    public static IReadOnlyList<string> Filters { get; } = ["Apps", "System apps", "Frameworks", "All packages"];

    private readonly IStoreAppService _apps;
    private readonly IDialogService _dialogs;
    private readonly IInfoBarService _infoBar;
    private readonly ILogger<StoreAppsViewModel> _logger;
    private readonly IStatusService _status;
    private readonly List<StoreAppRowViewModel> _all = [];

    public StoreAppsViewModel(
        IStoreAppService apps, IDialogService dialogs, IInfoBarService infoBar, IStatusService status, ILogger<StoreAppsViewModel> logger)
    {
        _status = status;
        _apps = apps;
        _dialogs = dialogs;
        _infoBar = infoBar;
        _logger = logger;
        Title = "Store apps";
    }

    /// <summary>Raised after anything was removed, so the Restoration tab can show it.</summary>
    public event EventHandler? Changed;

    public ObservableCollection<StoreAppRowViewModel> Filtered { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int FilterIndex { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveAllBloatwareCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnFilterIndexChanged(int value) => ApplyFilter();

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var apps = await _apps.ListAsync();
            _all.Clear();
            foreach (var app in apps)
            {
                _all.Add(new StoreAppRowViewModel(app, RemoveCommand));
            }

            ApplyFilter();
            foreach (var row in _all)
            {
                row.LoadIcon();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not list Store packages");
            _infoBar.ShowError("Store apps", $"Could not list Store packages: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var matches = _all.Where(row => FilterIndex switch
        {
            0 => row.Model.Kind == StoreAppKind.App,
            1 => row.Model.Kind == StoreAppKind.System,
            2 => row.Model.Kind == StoreAppKind.Framework,
            _ => true,
        });

        if (query.Length > 0)
        {
            matches = matches.Where(row =>
                row.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                row.PackageName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        Filtered.Clear();
        foreach (var row in matches)
        {
            Filtered.Add(row);
        }

        StatusText = $"Showing {Filtered.Count} of {_all.Count} package{(_all.Count == 1 ? string.Empty : "s")}.";
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task RemoveAsync(StoreAppRowViewModel? row)
    {
        if (row is null || IsBusy || !row.IsRemovable)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            $"Remove {row.DisplayName}?",
            $"It is removed for every user on this PC and Windows will not reinstall it, not even with a feature update. Restoration can bring it back.",
            "Remove",
            "Cancel");

        if (confirmed)
        {
            await RemoveManyAsync([row]);
        }
    }

    private bool CanRemoveAll() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRemoveAll))]
    private async Task RemoveAllBloatwareAsync()
    {
        var bloat = _all.Where(row => AppxKeepList.IsBloatware(row.Model)).ToList();
        if (bloat.Count == 0)
        {
            _infoBar.ShowInfo("Store apps", "Nothing left to remove: every remaining app is on the keep-list.");
            return;
        }

        var names = string.Join(Environment.NewLine, bloat.Select(row => $"    {row.DisplayName}"));
        var confirmed = await _dialogs.ConfirmAsync(
            $"Remove {bloat.Count} app{(bloat.Count == 1 ? string.Empty : "s")}?",
            $"AkariOS-Ultimate's bloatware removal: every app except the codecs, Photos, Paint, Notepad, Windows Security, the Start menu and taskbar, NVIDIA Control Panel, Settings, the Microsoft Store and App Installer. Xbox and Gaming Services go too, which Game Pass needs. Windows will not reinstall them; Restoration can.{Environment.NewLine}{Environment.NewLine}{names}",
            "Remove all",
            "Cancel");

        if (confirmed)
        {
            await RemoveManyAsync(bloat);
        }
    }

    private async Task RemoveManyAsync(IReadOnlyList<StoreAppRowViewModel> rows)
    {
        IsBusy = true;
        var failed = new List<(string Name, string Error)>();
        try
        {
            foreach (var row in rows)
            {
                try
                {
                    StatusText = $"Removing {row.DisplayName}...";
                    _logger.LogInformation("Removing {Package}", row.Model.FullName);
                    using (_status.Begin($"Removing {row.DisplayName}"))
                    {
                        await _apps.RemoveAsync(row.Model);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Removing {Package} failed", row.Model.FullName);
                    failed.Add((row.DisplayName, ex.Message));
                }
            }
        }
        finally
        {
            IsBusy = false;
        }

        var removed = rows.Count - failed.Count;
        if (failed.Count == 0)
        {
            _infoBar.ShowSuccess("Store apps", $"Removed {removed} app{(removed == 1 ? string.Empty : "s")}.");
        }
        else
        {
            var report = new StringBuilder($"Removed {removed}, failed {failed.Count}:");
            foreach (var (name, error) in failed)
            {
                report.AppendLine().Append($"    {name}: {error}");
            }

            _infoBar.ShowWarning("Store apps", report.ToString());
            await _dialogs.ShowInfoAsync("Store apps", report.ToString());
        }

        await LoadAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
