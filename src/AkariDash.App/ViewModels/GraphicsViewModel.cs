using System.Collections.ObjectModel;
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
/// where AkariOS asks, and what the script writes is shown in the log below the rows.
/// </summary>
public sealed partial class GraphicsViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly IStatusService _status;
    private readonly ILogger<GraphicsViewModel> _logger;

    public GraphicsViewModel(IMachine machine, IDialogService dialogs, IStatusService status, ILogger<GraphicsViewModel> logger)
    {
        _dialogs = dialogs;
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

    /// <summary>Every line the scripts have written this session, newest last.</summary>
    public ObservableCollection<string> Log { get; } = [];

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
            Log.Add($"Dry Run: {action.Script} was not run.");
            return;
        }

        IsBusy = true;
        using var job = _status.Begin(action.Description);
        Log.Add($"> {action.Description}");
        var lines = new Progress<string>(Log.Add);
        try
        {
            await EmbeddedScripts.RunGraphicsAsync(action.Script, ((IProgress<string>)lines).Report);
            _logger.LogInformation("Ran {Script}", action.Script);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Script} failed", action.Script);
            Log.Add($"{action.Script} could not run: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
