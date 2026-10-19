using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AkariDash.App.Services;
using AkariDash.Core.Gpus;
using AkariDash.Core.Machine;
using AkariDash.Framework.Navigation;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>One line of a card's details: what it is, and what Windows says.</summary>
public sealed record GpuFact(string Label, string Value);

/// <summary>
/// One graphics card's page, after Vain Toolbox's GPU page: the card's details and Restart GPU. The
/// card comes in as the navigation parameter from its GPUs entry.
/// </summary>
public sealed partial class GpuCardViewModel(
    IDialogService dialogs,
    IInfoBarService infoBar,
    IStatusService status,
    ILogger<GpuCardViewModel> logger) : ViewModelBase, INavigationAware
{
    private GpuCard? _card;

    [ObservableProperty]
    public partial string Name { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Vendor { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Kind { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<GpuFact> Facts { get; private set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
    public partial bool IsBusy { get; private set; }

    public void OnNavigatedTo(object? parameter)
    {
        if (parameter is not GpuCard card)
        {
            return;
        }

        _card = card;
        Name = card.Name;
        Vendor = card.Vendor.DisplayName();
        Kind = card.Kind == GpuKind.Integrated ? "iGPU" : "dGPU";
        const string unknown = "Not reported";
        Facts =
        [
            new("Driver version", card.DriverVersion ?? unknown),
            new("Driver date", card.DriverDate?.ToString("d MMMM yyyy") ?? unknown),
            new("Video memory", card.VideoMemory ?? unknown),
            new("PCIe link", card.PcieLink ?? unknown),
            new("Device instance", card.InstanceId),
        ];
    }

    public void OnNavigatedFrom()
    {
    }

    private bool CanRestart() => _card is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRestart))]
    private async Task RestartAsync()
    {
        if (_card is not { } card ||
            !await dialogs.ConfirmAsync(
                "Restart GPU?",
                $"The driver of {card.Name} is stopped and started again. The screen goes black for a few seconds.",
                "Restart",
                "Cancel"))
        {
            return;
        }

        if (BuildInfo.IsDryRunOnly)
        {
            infoBar.ShowInfo("Restart GPU", $"Dry Run: {card.Name} was not restarted.");
            return;
        }

        IsBusy = true;
        try
        {
            bool rebootNeeded;
            using (status.Begin("Restarting GPU..."))
            {
                rebootNeeded = await WindowsGpuCards.RestartAsync(card);
            }

            if (rebootNeeded)
            {
                infoBar.ShowWarning("GPU restart", "Windows needs a restart to finish restarting the GPU.");
            }
            else
            {
                infoBar.ShowSuccess("GPU restarted", "The GPU driver has been restarted.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Restarting {Card} failed", card.Name);
            await dialogs.ShowErrorAsync("Restart GPU", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
