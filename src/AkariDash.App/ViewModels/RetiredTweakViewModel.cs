using CommunityToolkit.Mvvm.Input;
using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;

namespace AkariDash.App.ViewModels;

/// <summary>One row of Home's card for Tweaks no longer in Akari-Dash: what Undo would put back.</summary>
public sealed partial class RetiredTweakViewModel(RetiredTweak retired, Func<RetiredTweak, Task> undo)
{
    public string Id => retired.Id;

    /// <summary>One line per saved value: where it is, and what Undo puts back there.</summary>
    public string Changes { get; } = string.Join(
        Environment.NewLine,
        retired.OriginalValues.Select(original => $"{original.Location} → {MachineValue.Describe(original.Value)}"));

    [RelayCommand]
    private Task UndoAsync() => undo(retired);
}
