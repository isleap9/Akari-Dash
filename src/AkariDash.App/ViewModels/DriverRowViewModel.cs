using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkariDash.Core.Drivers;

namespace AkariDash.App.ViewModels;

/// <summary>Where one Drivers row is in its run.</summary>
public enum DriverRowState
{
    /// <summary>Nothing has happened yet; the row can be pressed.</summary>
    Ready,

    /// <summary>Waiting for the queue to reach it.</summary>
    Queued,

    /// <summary>Running now.</summary>
    Working,

    /// <summary>Installed, and configured when the row said so.</summary>
    Done,

    /// <summary>Installed, but Windows wants a restart before it is usable.</summary>
    NeedsRestart,

    /// <summary>Installed, but the settings the row promised were not all applied.</summary>
    DoneWithoutSettings,

    /// <summary>The vendor's own installer or page was opened instead.</summary>
    HandedOff,

    /// <summary>It did not work. The reason is in the info bar.</summary>
    Failed,
}

/// <summary>A driver installer found in the drop folder, as a Debloat row lists it.</summary>
public sealed record DroppedInstaller(string Path)
{
    public string Name => System.IO.Path.GetFileName(Path);

    public override string ToString() => Name;
}

/// <summary>
/// One row on the Drivers page: what it does, how careful to be with it, and where its run is.
/// The row runs nothing itself; the page passes in what pressing it does, and queues it.
/// </summary>
public sealed partial class DriverRowViewModel : ObservableObject
{
    private readonly Func<DriverRowViewModel, Task> _run;

    public DriverRowViewModel(DriverRow row, Func<DriverRowViewModel, Task> run)
    {
        Model = row;
        _run = run;
    }

    public DriverRow Model { get; }

    public string Title => Model.Title;

    public string Description => Model.Description;

    public string RiskLabel => Model.Risk switch
    {
        DriverRisk.Advanced => "Advanced",
        DriverRisk.Caution => "Caution",
        _ => "Safe",
    };

    /// <summary>Only a Debloat row takes a file, from the drop folder.</summary>
    public bool IsDebloat => Model.Kind == DriverStepKind.Debloat;

    /// <summary>The installers in the drop folder that are this vendor's own. Empty on the other rows.</summary>
    public ObservableCollection<DroppedInstaller> Installers { get; } = [];

    [ObservableProperty]
    public partial DroppedInstaller? SelectedInstaller { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    [NotifyPropertyChangedFor(nameof(IsRunnable))]
    [NotifyPropertyChangedFor(nameof(IsWorking))]
    public partial DriverRowState State { get; set; } = DriverRowState.Ready;

    public string StateText => State switch
    {
        DriverRowState.Queued => "Queued",
        DriverRowState.Working => "Working…",
        DriverRowState.Done => "Done",
        DriverRowState.NeedsRestart => "Installed — restart to finish",
        DriverRowState.DoneWithoutSettings => "Installed — settings not applied",
        DriverRowState.HandedOff => "Opened the installer",
        DriverRowState.Failed => "Could not install",
        _ => string.Empty,
    };

    /// <summary>Pressable when nothing is running for it: once it is queued, pressing again would install twice.</summary>
    public bool IsRunnable => State is not (DriverRowState.Queued or DriverRowState.Working);

    public bool IsWorking => State == DriverRowState.Working;

    /// <summary>Replaces the installer list, keeping the pick when that file is still there.</summary>
    internal void SetInstallers(IEnumerable<DroppedInstaller> installers)
    {
        var kept = SelectedInstaller;
        Installers.Clear();
        foreach (var installer in installers)
        {
            Installers.Add(installer);
        }

        SelectedInstaller = Installers.FirstOrDefault(installer => installer == kept) ?? Installers.FirstOrDefault();
    }

    [RelayCommand]
    private Task RunAsync() => _run(this);
}
