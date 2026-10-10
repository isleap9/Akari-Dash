using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>
/// One Tweak row. A two-Option Tweak is shown as a toggle whose off side is its first Option
/// and whose on side is its second.
/// </summary>
public sealed partial class TweakViewModel : ViewModelBase
{
    private readonly DeclaredTweak _tweak;
    private readonly Action<DeclaredTweak, TweakOption> _chooseOption;
    private readonly Action<DeclaredTweak> _undo;
    private readonly TweakOption? _driftedFrom;
    private bool _isOn;

    /// <param name="isApplied">Whether Akari-Dash has applied this Tweak (and so can Undo it).</param>
    /// <param name="chooseOption">Called when the user picks an Option; the row is rebuilt afterwards.</param>
    /// <param name="undo">Called when the user asks to Undo; the row is rebuilt afterwards.</param>
    public TweakViewModel(
        DeclaredTweak tweak,
        LiveState state,
        bool isApplied,
        Action<DeclaredTweak, TweakOption> chooseOption,
        Action<DeclaredTweak> undo)
    {
        _tweak = tweak;
        _chooseOption = chooseOption;
        _undo = undo;
        IsApplied = isApplied;

        Title = tweak.Title;
        Description = tweak.Description;
        OffLabel = tweak.Options[0].Label;
        OnLabel = tweak.Options[1].Label;

        // A drifted row shows what the machine is actually in now.
        var actual = state;
        if (state is LiveState.Drifted drifted)
        {
            _driftedFrom = drifted.Expected;
            DriftText = $"Akari-Dash set {drifted.Expected.Label}, but it is now {Describe(drifted.Actual)}.";
            actual = drifted.Actual;
        }

        _isOn = actual is LiveState.InOption inOption && inOption.Option == tweak.Options[1];

        if (actual is LiveState.Custom custom)
        {
            // Drift takes precedence: a drifted Tweak shows its value in the Drift badge instead.
            IsCustom = !IsDrifted;
            IsToggleVisible = false;
            CustomValue = Describe(custom);
        }
    }

    public string Description { get; }

    public string OffLabel { get; }

    public string OnLabel { get; }

    /// <summary>Which side the toggle shows; flipping it asks to put the Tweak into that Option.</summary>
    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (SetProperty(ref _isOn, value))
            {
                _chooseOption(_tweak, _tweak.Options[value ? 1 : 0]);
            }
        }
    }

    public bool IsCustom { get; }

    /// <summary>The Tweak is no longer in the Option Akari-Dash last applied; Re-apply is offered.</summary>
    public bool IsDrifted => _driftedFrom is not null;

    /// <summary>What Akari-Dash applied and what the machine is in now, when the Tweak has drifted.</summary>
    public string? DriftText { get; }

    /// <summary>Undo is offered only on Tweaks Akari-Dash has applied.</summary>
    public bool IsApplied { get; }

    /// <summary>The toggle is hidden while the live values match no Option: neither of its sides is true.</summary>
    public bool IsToggleVisible { get; } = true;

    /// <summary>The actual live value(s) when the Tweak is Custom.</summary>
    public string? CustomValue { get; }

    [RelayCommand]
    private void Undo() => _undo(_tweak);

    /// <summary>Puts a drifted Tweak back into the Option Akari-Dash last applied.</summary>
    [RelayCommand]
    private void Reapply()
    {
        if (_driftedFrom is not null)
        {
            _chooseOption(_tweak, _driftedFrom);
        }
    }

    private static string Describe(LiveState state) => state switch
    {
        LiveState.InOption inOption => inOption.Option.Label,
        LiveState.Custom custom => Describe(custom),
        _ => throw new UnreachableException($"Drift is never in another state: {state}"),
    };

    private static string Describe(LiveState.Custom custom) =>
        custom.Values.Count == 1
            ? RegistryValue.Describe(custom.Values[0].Value)
            : string.Join("; ", custom.Values.Select(v => $"{v.Target.Location} = {RegistryValue.Describe(v.Value)}"));
}
