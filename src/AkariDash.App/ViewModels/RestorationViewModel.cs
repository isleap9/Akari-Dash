using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AkariDash.Core.Apps;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>One Restoration card: an app from the curated list that is not on this PC.</summary>
public sealed partial class RestorableAppViewModel : ObservableObject
{
    public RestorableAppViewModel(RestorableApp app, IAsyncRelayCommand<RestorableAppViewModel?> restore)
    {
        Model = app;
        RestoreCommand = restore;
    }

    public RestorableApp Model { get; }

    public IAsyncRelayCommand<RestorableAppViewModel?> RestoreCommand { get; }

    public string Name => Model.Name;

    public string Description => Model.Description;

    /// <summary>Whether a Restore can work: a copy left on disk is found at restore time, so only the Store fallback is known now.</summary>
    public string Status => Model.StoreId is null ? "Not installed · not in the Store any more" : "Not installed";

    [ObservableProperty]
    public partial bool IsRestoring { get; set; }
}

/// <summary>
/// The Restoration tab, after FSOS-X's: every app on the curated list that is not installed, with a
/// Restore button. That covers apps removed by anything, not only by Akari-Dash.
/// </summary>
public sealed partial class RestorationViewModel : ViewModelBase
{
    private readonly IStoreAppService _apps;
    private readonly IInfoBarService _infoBar;
    private readonly ILogger<RestorationViewModel> _logger;

    public RestorationViewModel(IStoreAppService apps, IInfoBarService infoBar, ILogger<RestorationViewModel> logger)
    {
        _apps = apps;
        _infoBar = infoBar;
        _logger = logger;
        Title = "Restoration";
    }

    /// <summary>Raised after an app came back, so the Store apps tab lists it again.</summary>
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
            foreach (var app in RestorableApps.All.Where(app => !installed.Contains(app.PackageName)))
            {
                Missing.Add(new RestorableAppViewModel(app, RestoreCommand));
            }

            StatusText = Missing.Count == 0
                ? "Every app on this list is installed."
                : $"{Missing.Count} app{(Missing.Count == 1 ? " is" : "s are")} not installed.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not list Store packages for Restoration");
            _infoBar.ShowError("Restoration", $"Could not list Store packages: {ex.Message}");
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
            _logger.LogInformation("Restoring {Package}", card.Model.PackageName);
            await _apps.RestoreAsync(card.Model);
            _infoBar.ShowSuccess("Restoration", $"{card.Name} is back.");
            await LoadAsync();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restoring {Package} failed", card.Model.PackageName);
            _infoBar.ShowError("Restoration", ex.Message);
        }
        finally
        {
            card.IsRestoring = false;
            IsBusy = false;
        }
    }
}
