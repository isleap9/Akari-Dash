using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AkariDash.Core.Apps;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>One Legacy apps card.</summary>
public sealed partial class LegacyAppViewModel : ObservableObject
{
    public LegacyAppViewModel(LegacyApp app, IAsyncRelayCommand<LegacyAppViewModel?> remove, IAsyncRelayCommand<LegacyAppViewModel?> install)
    {
        Model = app;
        RemoveCommand = remove;
        InstallCommand = install;
    }

    public LegacyApp Model { get; }

    public IAsyncRelayCommand<LegacyAppViewModel?> RemoveCommand { get; }

    public IAsyncRelayCommand<LegacyAppViewModel?> InstallCommand { get; }

    public string Name => Model.Name;

    public string Description => Model.Description;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status), nameof(IsMissing))]
    public partial bool IsInstalled { get; set; }

    public bool IsMissing => !IsInstalled;

    public string Status => IsInstalled ? "Installed" : "Not installed";

    [ObservableProperty]
    public partial bool IsWorking { get; set; }
}

/// <summary>
/// The Legacy apps tab: AkariOS-Ultimate's removals for what the other tabs cannot take away, each with
/// its reinstall beside it.
/// </summary>
public sealed partial class LegacyAppsViewModel : ViewModelBase
{
    private readonly ILegacyAppService _apps;
    private readonly IDialogService _dialogs;
    private readonly IInfoBarService _infoBar;
    private readonly ILogger<LegacyAppsViewModel> _logger;

    public LegacyAppsViewModel(ILegacyAppService apps, IDialogService dialogs, IInfoBarService infoBar, ILogger<LegacyAppsViewModel> logger)
    {
        _apps = apps;
        _dialogs = dialogs;
        _infoBar = infoBar;
        _logger = logger;
        Title = "Legacy apps";
        foreach (var app in LegacyApps.All)
        {
            Apps.Add(new LegacyAppViewModel(app, RemoveCommand, InstallCommand));
        }
    }

    public ObservableCollection<LegacyAppViewModel> Apps { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public void Load()
    {
        foreach (var card in Apps)
        {
            card.IsInstalled = _apps.IsInstalled(card.Model);
        }
    }

    [RelayCommand]
    private async Task RemoveAsync(LegacyAppViewModel? card)
    {
        if (card is null || IsBusy)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync($"Remove {card.Name}?", card.Model.Warning, "Remove", "Cancel");
        if (confirmed)
        {
            await RunAsync(card, "Removing", () => _apps.RemoveAsync(card.Model), $"{card.Name} is removed.");
        }
    }

    [RelayCommand]
    private Task InstallAsync(LegacyAppViewModel? card) =>
        card is null || IsBusy
            ? Task.CompletedTask
            : RunAsync(card, "Installing", () => _apps.InstallAsync(card.Model), $"{card.Name} is installed.");

    private async Task RunAsync(LegacyAppViewModel card, string doing, Func<Task> work, string done)
    {
        IsBusy = true;
        card.IsWorking = true;
        try
        {
            _logger.LogInformation("{Doing} {App}", doing, card.Model.Id);
            await work();
            _infoBar.ShowSuccess("Legacy apps", done);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Doing} {App} failed", doing, card.Model.Id);
            _infoBar.ShowError("Legacy apps", ex.Message);
        }
        finally
        {
            card.IsInstalled = _apps.IsInstalled(card.Model);
            card.IsWorking = false;
            IsBusy = false;
        }
    }
}
