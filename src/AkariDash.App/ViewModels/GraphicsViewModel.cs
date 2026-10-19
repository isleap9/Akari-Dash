using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AkariDash.App.Services;
using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>One button of a Drivers row: runs its AkariOS-Ultimate script through the page.</summary>
public sealed class GraphicsActionViewModel(GraphicsAction action, Func<GraphicsAction, Task> run)
{
    public string Label { get; } = action.Label;

    public string Description { get; } = action.Description;

    public IAsyncRelayCommand RunCommand { get; } = new AsyncRelayCommand(() => run(action));
}

/// <summary>One row of the Drivers page.</summary>
public sealed class GraphicsEntryViewModel(GraphicsEntry entry, Func<GraphicsAction, Task> run)
{
    public string Name { get; } = entry.Name;

    public string Description { get; } = entry.Description;

    public IReadOnlyList<GraphicsActionViewModel> Actions { get; } =
        entry.Actions.Select(action => new GraphicsActionViewModel(action, run)).ToList();
}

/// <summary>
/// The Drivers page: AkariOS-Ultimate's Graphics category for the cards in this PC (see
/// <see cref="GraphicsEntries"/>). An Action surface (ADR-0004): one script at a time, asked first
/// where AkariOS asks, with the status bar saying what runs. What a script writes goes to the app's
/// log file, not the page. DDU is prepared by its script and then queued for the safe-mode companion
/// Disable Defender uses, which restarts the PC into safe mode to run it.
/// </summary>
public sealed partial class GraphicsViewModel : ViewModelBase
{
    private readonly IMachine _machine;
    private readonly IDialogService _dialogs;
    private readonly IInfoBarService _infoBar;
    private readonly IStatusService _status;
    private readonly ILogger<GraphicsViewModel> _logger;

    public GraphicsViewModel(
        IMachine machine,
        IDialogService dialogs,
        IInfoBarService infoBar,
        IStatusService status,
        ILogger<GraphicsViewModel> logger)
    {
        _machine = machine;
        _dialogs = dialogs;
        _infoBar = infoBar;
        _status = status;
        _logger = logger;
        Title = "Drivers";

        var vendors = machine.GpuVendors();
        Entries = GraphicsEntries.For(vendors).Select(entry => new GraphicsEntryViewModel(entry, RunAsync)).ToList();
        CardsText = vendors.Count == 0
            ? "No NVIDIA, AMD or Intel graphics card was found, so only the rows for every card are shown."
            : $"Showing the rows for {string.Join(" and ", vendors.Order().Select(vendor => vendor.DisplayName()))}.";
    }

    public IReadOnlyList<GraphicsEntryViewModel> Entries { get; }

    /// <summary>Which cards the rows are for.</summary>
    public string CardsText { get; }

    /// <summary>A script is running; the buttons wait for it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; set; }

    public bool IsIdle => !IsBusy;

    private async Task RunAsync(GraphicsAction action)
    {
        if (IsBusy)
        {
            return;
        }

        if (action.Confirm is { } question && !await _dialogs.ConfirmAsync(action.Description, question, "Continue", "Cancel"))
        {
            return;
        }

        if (BuildInfo.IsDryRunOnly)
        {
            _infoBar.ShowInfo(action.Description, $"Dry Run: {action.Script} was not run.");
            return;
        }

        IsBusy = true;
        try
        {
            using (_status.Begin(action.Description))
            {
                await EmbeddedScripts.RunGraphicsAsync(action.Script, line => _logger.LogInformation("{Script}: {Line}", action.Script, line));
            }

            if (action.SafeModeOption is { } mode)
            {
                if (!File.Exists(DriverClean.Executable))
                {
                    throw new InvalidOperationException("DDU could not be downloaded, so nothing was removed. Check the internet connection and try again.");
                }

                _machine.StageSafeModePass(DriverClean.Pass(mode));
                _infoBar.ShowWarning(action.Description, "The PC is restarting into safe mode to run DDU.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Script} failed", action.Script);
            await _dialogs.ShowErrorAsync(action.Description, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
