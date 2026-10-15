using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using AkariDash.Core.Apps;

namespace AkariDash.App.ViewModels;

/// <summary>Where one Downloads tile is in its install.</summary>
public enum AppTileState
{
    /// <summary>We have not had an answer from winget yet.</summary>
    Checking,

    /// <summary>winget could not be asked, so this tile does not know - and will not guess.</summary>
    Unknown,

    /// <summary>Not on this machine, so the tile can be picked.</summary>
    NotInstalled,

    /// <summary>Waiting for the queue to reach it.</summary>
    Queued,

    /// <summary>winget is working on it now.</summary>
    Installing,

    /// <summary>On the machine.</summary>
    Installed,

    /// <summary>On the machine, but Windows wants a restart before it is usable.</summary>
    NeedsRestart,

    /// <summary>The install did not work. The reason is in the info bar.</summary>
    Failed,
}

/// <summary>
/// One app on the Downloads page: the app, its avatar square, whether it is already installed, and
/// whether the user has picked it. The tile installs nothing itself - clicking the card says "this
/// one", and the page's one Install button takes the picked tiles away.
/// </summary>
public sealed partial class AppTileViewModel : ObservableObject
{
    private readonly Action _selectionChanged;

    public AppTileViewModel(InstallableApp app, Action selectionChanged)
    {
        Model = app;
        _selectionChanged = selectionChanged;
    }

    public InstallableApp Model { get; }

    public string Name => Model.Name;

    public string Publisher => Model.Publisher;

    public string Description => Model.Description;

    /// <summary>The colour of the avatar square, as a hex string.</summary>
    public string AccentColor => Model.AccentColor;

    /// <summary>The letter in the avatar square: "7" for 7-Zip, "S" for Steam.</summary>
    public string Initial => Name[..1].ToUpperInvariant();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    [NotifyPropertyChangedFor(nameof(IsInstallable))]
    [NotifyPropertyChangedFor(nameof(IsInstalling))]
    [NotifyCanExecuteChangedFor(nameof(ToggleSelectionCommand))]
    public partial AppTileState State { get; set; } = AppTileState.Checking;

    /// <summary>Picked in the grid, waiting for the page's one Install button.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HighlightThickness))]
    [NotifyPropertyChangedFor(nameof(AutomationName))]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => _selectionChanged();

    /// <summary>
    /// The whole-card button's own name. The card's text sits under it rather than in it, so the button
    /// would otherwise be an unnamed control to a screen reader.
    /// </summary>
    public string AutomationName => IsSelected ? $"{Name}, picked" : $"Pick {Name}";

    /// <summary>
    /// The accent outline a picked tile wears, drawn over the card the way a tweak row draws one over
    /// a search hit. Zero thickness is no outline, so the ring needs no second property of its own.
    /// </summary>
    public Thickness HighlightThickness => new(IsSelected ? 2 : 0);

    /// <summary>
    /// Clicking anywhere on the card is the picking - the whole tile is the control, not a tick box
    /// inside it. A tile that cannot be installed does nothing, and does not light up under the
    /// pointer either.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsInstallable))]
    private void ToggleSelection() => IsSelected = !IsSelected;

    /// <summary>What the caption beside the button says.</summary>
    public string StateText => State switch
    {
        AppTileState.Installed => "Installed",
        AppTileState.NeedsRestart => "Installed — restart to finish",
        AppTileState.Queued => "Queued",
        AppTileState.Installing => "Installing…",
        AppTileState.Failed => "Could not install",
        AppTileState.NotInstalled => "Not installed",
        AppTileState.Unknown => "Unknown",
        _ => string.Empty,
    };

    /// <summary>Live only where installing is the next thing to do - and so, only where a click means something.</summary>
    public bool IsInstallable => State is AppTileState.NotInstalled or AppTileState.Failed;

    /// <summary>This tile's own spinner, on while winget is working on it.</summary>
    public bool IsInstalling => State is AppTileState.Installing;
}
