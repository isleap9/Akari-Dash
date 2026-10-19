using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AkariDash.App.Services;
using AkariDash.Core.Gpus;
using AkariDash.Core.Machine;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>
/// One column of the card's header, as Vain Toolbox lays them out: a small label over its value.
/// <paramref name="Accent"/> shows the value in the accent colour, as Vain does for Driver Installed.
/// </summary>
public sealed record GpuFact(string Label, string Value, bool Accent = false)
{
    public bool Plain => !Accent;
}

/// <summary>
/// The GPUs page, after Vain Toolbox's GPU page, in tabs like Customize and UWP Apps. The Main tab shows
/// the picked card's header as Vain lays it out, with Restart GPU top right; with several cards a picker
/// chooses which.
/// </summary>
public sealed partial class GpusViewModel : ViewModelBase
{
    private readonly IMachine _machine;
    private readonly IDialogService _dialogs;
    private readonly IInfoBarService _infoBar;
    private readonly IStatusService _status;
    private readonly ILogger<GpusViewModel> _logger;

    public GpusViewModel(IMachine machine, IDialogService dialogs, IInfoBarService infoBar, IStatusService status, ILogger<GpusViewModel> logger)
    {
        _machine = machine;
        _dialogs = dialogs;
        _infoBar = infoBar;
        _status = status;
        _logger = logger;
        Title = "GPUs";
        Settings = new GpuSettingsViewModel(machine, dialogs, infoBar, status, logger);

        Cards = WindowsGpuCards.Read();
        SelectedCard = Cards.FirstOrDefault();
    }

    /// <summary>The Settings tab, for the picked card's displays.</summary>
    public GpuSettingsViewModel Settings { get; }

    private bool _settingsOpened;

    /// <summary>Reads the Settings tab the first time it is opened; NVAPI takes a moment to list the displays.</summary>
    public void OpenSettings()
    {
        if (!_settingsOpened)
        {
            _settingsOpened = true;
            Settings.Load(SelectedCard);
        }
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

    /// <summary>Vain Toolbox's Registry Settings for the picked card: NVIDIA cards only.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<NvidiaRegistryGroupViewModel> RegistryGroups { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasRegistrySettings { get; private set; }

    [ObservableProperty]
    public partial string RegistrySearch { get; set; } = string.Empty;

    partial void OnRegistrySearchChanged(string value) => FilterRegistry();

    private void FilterRegistry()
    {
        var search = RegistrySearch.Trim();
        foreach (var group in RegistryGroups)
        {
            foreach (var row in group.Rows)
            {
                row.IsVisible = search.Length == 0 || row.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
            }

            group.IsVisible = group.Rows.Any(row => row.IsVisible);
        }
    }

    private void LoadRegistry(GpuCard card)
    {
        if (card.Vendor != GpuVendor.Nvidia || card.RegistryKey is not { } key)
        {
            RegistryGroups = [];
            HasRegistrySettings = false;
            return;
        }

        void Failed(string message) => _infoBar.ShowError("Registry Settings", message);

        RegistryGroups = NvidiaRegistrySettings.All
            .GroupBy(setting => setting.Group)
            .Select(group => new NvidiaRegistryGroupViewModel(
                group.Key,
                group.Select(setting => new NvidiaRegistryRowViewModel(setting, key, _machine, Failed)).ToList()))
            .ToList();
        HasRegistrySettings = true;
        FilterRegistry();
    }

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
            new("Manufacturer", card.Manufacturer),
            new("Status", card.HasDriver ? "Driver Installed" : "No Driver", Accent: card.HasDriver),
            new("Hardware ID", card.HardwareId ?? unknown),
            new("Driver Version", card.DriverVersion ?? unknown),
            new("Driver Date", card.DriverDate?.ToString("d MMM yyyy") ?? unknown),
            new("Video Memory", card.VideoMemory ?? unknown),
            new("PCIe Link", card.PcieLink ?? unknown),
            new("Registry Key", card.RegistryKey ?? unknown),
        ];
        LoadRegistry(card);
        if (_settingsOpened)
        {
            Settings.Load(card);
        }
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
