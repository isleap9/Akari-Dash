using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AkariDash.App.Services;
using AkariDash.Core.Gpus;
using AkariDash.Core.Machine;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>One line of a card's details: what it is, and what Windows says.</summary>
public sealed record GpuFact(string Label, string Value);

/// <summary>
/// The GPUs page, after Vain Toolbox's GPU page, in tabs like Customize and UWP Apps. The Graphics card
/// tab shows the picked card's details and Restart GPU; with several cards a picker chooses which.
/// </summary>
public sealed partial class GpusViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly IInfoBarService _infoBar;
    private readonly IStatusService _status;
    private readonly ILogger<GpusViewModel> _logger;

    public GpusViewModel(IDialogService dialogs, IInfoBarService infoBar, IStatusService status, ILogger<GpusViewModel> logger)
    {
        _dialogs = dialogs;
        _infoBar = infoBar;
        _status = status;
        _logger = logger;
        Title = "GPUs";

        Cards = WindowsGpuCards.Read();
        SelectedCard = Cards.FirstOrDefault();
    }

    /// <summary>Every NVIDIA, AMD and Intel card in this PC.</summary>
    public IReadOnlyList<GpuCard> Cards { get; }

    public bool HasCards => Cards.Count > 0;

    public bool HasNoCards => Cards.Count == 0;

    /// <summary>The picker is shown only when there is more than one card to pick from.</summary>
    public bool HasSeveralCards => Cards.Count > 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
    public partial GpuCard? SelectedCard { get; set; }

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

    partial void OnSelectedCardChanged(GpuCard? value)
    {
        if (value is not { } card)
        {
            return;
        }

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

    private bool CanRestart() => SelectedCard is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRestart))]
    private async Task RestartAsync()
    {
        if (SelectedCard is not { } card ||
            !await _dialogs.ConfirmAsync(
                "Restart GPU?",
                $"The driver of {card.Name} is stopped and started again. The screen goes black for a few seconds.",
                "Restart",
                "Cancel"))
        {
            return;
        }

        if (BuildInfo.IsDryRunOnly)
        {
            _infoBar.ShowInfo("Restart GPU", $"Dry Run: {card.Name} was not restarted.");
            return;
        }

        IsBusy = true;
        try
        {
            bool rebootNeeded;
            using (_status.Begin("Restarting GPU..."))
            {
                rebootNeeded = await WindowsGpuCards.RestartAsync(card);
            }

            if (rebootNeeded)
            {
                _infoBar.ShowWarning("GPU restart", "Windows needs a restart to finish restarting the GPU.");
            }
            else
            {
                _infoBar.ShowSuccess("GPU restarted", "The GPU driver has been restarted.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restarting {Card} failed", card.Name);
            await _dialogs.ShowErrorAsync("Restart GPU", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
