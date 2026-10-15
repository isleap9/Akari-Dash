using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkariDash.Core.Drivers;

namespace AkariDash.App.ViewModels;

/// <summary>One choice inside a Group row, e.g. "NVIDIA" under "Driver install (updated)", with its own button.</summary>
public sealed partial class GraphicsOptionViewModel(
    GraphicsEntryViewModel entry,
    GraphicsOption option,
    string? unavailableReason,
    Func<GraphicsEntryViewModel, GraphicsOptionViewModel, Task> run) : ObservableObject
{
    public GraphicsOption Model { get; } = option;

    public string Name => Model.Name;

    public string Description => Model.Description;

    public string Button => Model.Button;

    /// <summary>False for a card that is not in this PC; the button says why in its tooltip.</summary>
    public bool IsAvailable => unavailableReason is null;

    public string? UnavailableReason => unavailableReason;

    [RelayCommand]
    private Task RunAsync() => run(entry, this);
}

/// <summary>
/// One row of the Graphics list, drawn as AkariOS draws it (Akari.ps1 New-Row): a dot that says
/// whether a Toggle was last optimized, the name and description, the risk, and the buttons.
/// </summary>
public sealed partial class GraphicsEntryViewModel : ObservableObject
{
    private readonly Func<GraphicsEntryViewModel, bool, Task> _toggle;
    private readonly Func<GraphicsEntryViewModel, Task> _run;

    public GraphicsEntryViewModel(
        GraphicsEntry entry,
        bool? applied,
        string? unavailableReason,
        Func<GraphicsEntryViewModel, bool, Task> toggle,
        Func<GraphicsEntryViewModel, Task> run,
        Func<GraphicsEntryViewModel, GraphicsOption, string?> optionUnavailable,
        Func<GraphicsEntryViewModel, GraphicsOptionViewModel, Task> runOption)
    {
        Model = entry;
        _toggle = toggle;
        _run = run;
        Applied = applied;
        UnavailableReason = unavailableReason;
        Options = entry.Choices.Select(option => new GraphicsOptionViewModel(this, option, optionUnavailable(this, option), runOption)).ToList();
    }

    public GraphicsEntry Model { get; }

    public string Name => Model.Name;

    public string Description => Model.Description;

    public string Risk => Model.Risk.ToString();

    public bool IsSafe => Model.Risk == DriverRisk.Safe;

    public bool IsCaution => Model.Risk == DriverRisk.Caution;

    public bool IsAdvanced => Model.Risk == DriverRisk.Advanced;

    public bool IsToggle => Model.Kind == GraphicsEntryKind.Toggle;

    public bool IsAction => Model.Kind == GraphicsEntryKind.Action;

    public bool IsGroup => Model.Kind == GraphicsEntryKind.Group;

    public string Button => Model.Button;

    public IReadOnlyList<GraphicsOptionViewModel> Options { get; }

    /// <summary>False for a Toggle whose card is not in this PC; the buttons say why in their tooltip.</summary>
    public bool IsAvailable => UnavailableReason is null;

    public string? UnavailableReason { get; }

    /// <summary>What Optimize or Default last did here: true, false, or not known (never pressed).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOptimized))]
    [NotifyPropertyChangedFor(nameof(IsNotOptimized))]
    [NotifyPropertyChangedFor(nameof(DotOpacity))]
    public partial bool? Applied { get; set; }

    /// <summary>The dot is filled, and Optimize is the accent button, once Optimize has run.</summary>
    public bool IsOptimized => Applied == true;

    public bool IsNotOptimized => Applied != true;

    /// <summary>A faint dot when nothing is known, as AkariOS draws it.</summary>
    public double DotOpacity => Applied is null ? 0.35 : 1;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsWorking { get; set; }

    [RelayCommand]
    private void ToggleOptions() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private Task OptimizeAsync() => _toggle(this, true);

    [RelayCommand]
    private Task DefaultAsync() => _toggle(this, false);

    [RelayCommand]
    private Task RunAsync() => _run(this);
}
