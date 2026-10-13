using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AkariDash.Core.Apps;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>One Restoration card: a curated Store app or a legacy program that is not on this PC.</summary>
public sealed partial class RestorableAppViewModel : ObservableObject
{
    public RestorableAppViewModel(
        string id, string name, string description, string status, Func<Task> restore, IAsyncRelayCommand<RestorableAppViewModel?> command)
    {
        Id = id;
        Name = name;
        Description = description;
        Status = status;
        Restore = restore;
        RestoreCommand = command;
    }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public string Status { get; }

    /// <summary>How this card's app comes back: from the Store, or from AkariOS-Ultimate's installer.</summary>
    public Func<Task> Restore { get; }

    public IAsyncRelayCommand<RestorableAppViewModel?> RestoreCommand { get; }

    [ObservableProperty]
    public partial bool IsRestoring { get; set; }
}

/// <summary>
/// The Restoration tab, after FSOS-X's: every curated Store app and legacy program that is not
/// installed, with a Restore button. That covers apps removed by anything, not only by Akari-Dash.
/// </summary>
public sealed partial class RestorationViewModel : ViewModelBase
{
    private readonly IStoreAppService _apps;
    private readonly ILegacyAppService _legacy;
    private readonly IInfoBarService _infoBar;
    private readonly ILogger<RestorationViewModel> _logger;

    public RestorationViewModel(IStoreAppService apps, ILegacyAppService legacy, IInfoBarService infoBar, ILogger<RestorationViewModel> logger)
    {
        _apps = apps;
        _legacy = legacy;
        _infoBar = infoBar;
        _logger = logger;
        Title = "Restoration";
    }

    /// <summary>Raised after an app came back, so the other tabs list it again.</summary>
    public event EventHandler? Changed;

    public ObservableCollection<RestorableAppViewModel> Missing { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    public async Task LoadAsync()
    {
        try
        {
            var installed = (await _apps.ListAsync()).Select(app => app.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Missing.Clear();

            foreach (var app in LegacyApps.All.Where(app => !_legacy.IsInstalled(app)))
            {
                Missing.Add(new RestorableAppViewModel(
                    app.Id, app.Name, app.Description, "Not installed", () => _legacy.InstallAsync(app), RestoreCommand));
            }

            foreach (var app in RestorableApps.All.Where(app => !installed.Contains(app.PackageName)))
            {
                Missing.Add(new RestorableAppViewModel(
                    app.PackageName,
                    app.Name,
                    app.Description,
                    app.StoreId is null ? "Not installed · not in the Store any more" : "Not installed",
                    () => _apps.RestoreAsync(app),
                    RestoreCommand));
            }

            StatusText = Missing.Count == 0
                ? "Every app on this list is installed."
                : $"{Missing.Count} app{(Missing.Count == 1 ? " is" : "s are")} not installed.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not list apps for Restoration");
            _infoBar.ShowError("Restoration", $"Could not list installed apps: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RestoreAsync(RestorableAppViewModel? card)
    {
        if (card is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        card.IsRestoring = true;
        try
        {
            _logger.LogInformation("Restoring {App}", card.Id);
            await card.Restore();
            _infoBar.ShowSuccess("Restoration", $"{card.Name} is back.");
            await LoadAsync();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restoring {App} failed", card.Id);
            _infoBar.ShowError("Restoration", ex.Message);
        }
        finally
        {
            card.IsRestoring = false;
            IsBusy = false;
        }
    }
}
