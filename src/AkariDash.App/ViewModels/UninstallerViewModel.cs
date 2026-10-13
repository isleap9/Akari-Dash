using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using AkariDash.Core.Apps;
using AkariDash.Framework.Navigation;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>One row in the Uninstaller: the app plus its tick box.</summary>
public sealed partial class InstalledAppEntryViewModel : ObservableObject
{
    private readonly Action _selectionChanged;

    public InstalledAppEntryViewModel(InstalledApp app, bool isSelected, Action selectionChanged)
    {
        Model = app;
        IsSelected = isSelected;
        _selectionChanged = selectionChanged;
    }

    public InstalledApp Model { get; }

    public string Name => Model.Name;

    public string Publisher => string.IsNullOrWhiteSpace(Model.Publisher) ? Model.Kind.ToString() : Model.Publisher;

    /// <summary>The app's own icon, once <see cref="LoadIconAsync"/> has found one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIconMissing))]
    public partial ImageSource? Icon { get; set; }

    /// <summary>Whether the card shows the generic app glyph instead, as Windows does for an app with no icon.</summary>
    public bool IsIconMissing => Icon is null;

    /// <summary>
    /// Loads the icon on the UI thread, which images must be made on. A Store package's logo is an
    /// image file already; a Win32 program's is inside an exe, dll or ico, which the shell's thumbnail
    /// draws. Any failure leaves the glyph: a missing icon is never worth an error.
    /// </summary>
    public async Task LoadIconAsync()
    {
        if (Icon is not null || Model.IconPath is not { } path || !File.Exists(path))
        {
            return;
        }

        try
        {
            if (Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp")
            {
                Icon = new BitmapImage(new Uri(path)) { DecodePixelWidth = 40 };
                return;
            }

            var file = await StorageFile.GetFileFromPathAsync(path);
            using var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 40);
            var image = new BitmapImage();
            await image.SetSourceAsync(thumbnail);
            Icon = image;
        }
        catch
        {
            // Leave the glyph.
        }
    }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => _selectionChanged();
}

/// <summary>
/// The Uninstaller page: search, tick apps, remove them. An Action surface (ADR-0004): every
/// uninstall runs once, cannot be undone, and asks first.
/// </summary>
public sealed partial class UninstallerViewModel : ViewModelBase, INavigationAware
{
    private readonly IInstalledAppService _apps;
    private readonly IDialogService _dialogs;
    private readonly IInfoBarService _infoBar;
    private readonly ILogger<UninstallerViewModel> _logger;
    private readonly List<InstalledAppEntryViewModel> _all = [];
    private bool _suppressSelectAll;

    public UninstallerViewModel(
        IInstalledAppService apps,
        IDialogService dialogs,
        IInfoBarService infoBar,
        ILogger<UninstallerViewModel> logger)
    {
        _apps = apps;
        _dialogs = dialogs;
        _infoBar = infoBar;
        _logger = logger;
        Title = "Uninstaller";
    }

    /// <summary>Raised after an uninstall, so Restoration and Store apps catch up.</summary>
    public event EventHandler? Changed;

    public ObservableCollection<InstalledAppEntryViewModel> Filtered { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsWorking { get; set; }

    [ObservableProperty]
    public partial bool? IsSelectAllChecked { get; set; } = false;

    [ObservableProperty]
    public partial int SelectedCount { get; set; }

    [ObservableProperty]
    public partial string AppsFoundText { get; set; } = string.Empty;

    public bool HasSelection => SelectedCount > 0;

    public bool CanInteract => !IsLoading && !IsWorking;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanInteract));
        UninstallSelectedCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsWorkingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanInteract));
        UninstallSelectedCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasSelection));
        UninstallSelectedCommand.NotifyCanExecuteChanged();
    }

    public void OnNavigatedTo(object? parameter) => _ = LoadAsync();

    public void OnNavigatedFrom()
    {
    }

    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var apps = await Task.Run(() => _apps.ListAsync());
            _all.Clear();
            foreach (var app in apps)
            {
                _all.Add(new InstalledAppEntryViewModel(app, false, OnEntrySelectionChanged));
            }

            ApplyFilter();

            // Back on the UI thread here, which images need. Each card fills in its icon as it arrives.
            foreach (var entry in _all)
            {
                _ = entry.LoadIconAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not list installed apps");
            _infoBar.ShowError("Uninstaller", $"Could not list installed apps: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        IEnumerable<InstalledAppEntryViewModel> matches = _all;
        if (!string.IsNullOrWhiteSpace(query))
        {
            matches = _all.Where(entry =>
                entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                entry.Publisher.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        }

        Filtered.Clear();
        foreach (var entry in matches.OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Filtered.Add(entry);
        }

        AppsFoundText = $"{Filtered.Count} app{(Filtered.Count == 1 ? string.Empty : "s")} found.";
        RefreshSelectionState();
    }

    private void OnEntrySelectionChanged() => RefreshSelectionState();

    private void RefreshSelectionState()
    {
        SelectedCount = _all.Count(entry => entry.IsSelected);

        _suppressSelectAll = true;
        try
        {
            IsSelectAllChecked = SelectedCount == 0
                ? false
                : _all.All(entry => entry.IsSelected) ? true : (bool?)null;
        }
        finally
        {
            _suppressSelectAll = false;
        }
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        if (_suppressSelectAll)
        {
            return;
        }

        // Checked or indeterminate -> tick the visible rows; unchecked -> clear them.
        var select = IsSelectAllChecked != false;
        foreach (var entry in Filtered)
        {
            entry.IsSelected = select;
        }

        RefreshSelectionState();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand(CanExecute = nameof(CanUninstall))]
    private async Task UninstallSelectedAsync()
    {
        var selected = _all.Where(entry => entry.IsSelected).ToList();
        if (selected.Count == 0 || IsWorking)
        {
            return;
        }

        var names = string.Join(Environment.NewLine, selected.Select(entry => $"    {entry.Name}"));
        var warnings = string.Concat(selected
            .Select(entry => entry.Model.Legacy?.Warning)
            .OfType<string>()
            .Select(warning => $"{Environment.NewLine}{Environment.NewLine}{warning}"));
        var confirmed = await _dialogs.ConfirmAsync(
            $"Uninstall {selected.Count} app{(selected.Count == 1 ? string.Empty : "s")}?",
            $"{(selected.All(entry => entry.Model.Legacy is not null) ? string.Empty : "This cannot be undone. ")}The following will be removed:{Environment.NewLine}{Environment.NewLine}{names}{warnings}",
            "Uninstall",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        IsWorking = true;
        try
        {
            var failed = new List<(string Name, string Error)>();
            var succeeded = 0;

            foreach (var entry in selected)
            {
                try
                {
                    _logger.LogInformation("Uninstalling {App} ({Kind})", entry.Model.Id, entry.Model.Kind);
                    await _apps.UninstallAsync(entry.Model);
                    succeeded++;
                    _logger.LogInformation("Uninstalled {App}", entry.Model.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Uninstall of {App} failed", entry.Model.Id);
                    failed.Add((entry.Name, ex.Message));
                }
            }

            if (failed.Count == 0)
            {
                _infoBar.ShowSuccess("Uninstaller", $"Uninstalled {succeeded} app{(succeeded == 1 ? string.Empty : "s")}.");
            }
            else
            {
                var report = new StringBuilder($"Uninstalled {succeeded}, failed {failed.Count}:");
                foreach (var (name, error) in failed)
                {
                    report.AppendLine().Append($"    {name}: {error}");
                }

                _infoBar.ShowWarning("Uninstaller", report.ToString());
                await _dialogs.ShowInfoAsync("Uninstaller", report.ToString());
            }

            await LoadAsync();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsWorking = false;
            UninstallSelectedCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanUninstall() => SelectedCount > 0 && !IsWorking && !IsLoading;
}
