using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>
/// One Tweak row. A two-Option Tweak is shown as a toggle whose off side is its first Option
/// and whose on side is its second; a Tweak with more Options is shown as a selector.
/// </summary>
public sealed partial class TweakViewModel : ViewModelBase
{
    private readonly DeclaredTweak _tweak;
    private readonly Action<DeclaredTweak, TweakOption> _chooseOption;
    private readonly Action<DeclaredTweak> _undo;
    private readonly TweakOption? _driftedFrom;
    private bool _isOn;
    private TweakOption? _selectedOption;

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
        Options = tweak.Options;
        IsSelector = tweak.Options.Count > 2;
        RecommendedLabel = tweak.Recommended?.Label;
        ActivationText = tweak.Activation switch
        {
            Activation.Immediately => "Immediately",
            Activation.AfterSignOut => "After sign-out",
            Activation.AfterRestart => "After restart",
            _ => throw new UnreachableException($"Unknown Activation: {tweak.Activation}"),
        };
        IsDelayed = tweak.Activation != Activation.Immediately;

        if (state is LiveState.Unavailable unavailable)
        {
            UnavailableReason = unavailable.Reason;
        }

        // A drifted row shows what the machine is actually in now.
        var actual = state;
        if (state is LiveState.Drifted drifted)
        {
            _driftedFrom = drifted.Expected;
            DriftText = $"Akari-Dash set {drifted.Expected.Label}, but it is now {Describe(drifted.Actual)}.";
            actual = drifted.Actual;
        }

        _selectedOption = (actual as LiveState.InOption)?.Option;
        _isOn = _selectedOption == tweak.Options[1];

        if (actual is LiveState.Custom custom)
        {
            // Drift takes precedence: a drifted Tweak shows its value in the Drift badge instead.
            IsCustom = !IsDrifted;
            MatchesNoOption = true;
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

    /// <summary>A Tweak with three or more Options is picked from a list instead of a toggle.</summary>
    public bool IsSelector { get; }

    public IReadOnlyList<TweakOption> Options { get; }

    /// <summary>The Option the selector shows (none while Custom); picking one asks to put the Tweak into it.</summary>
    public TweakOption? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (value is not null && SetProperty(ref _selectedOption, value))
            {
                _chooseOption(_tweak, value);
            }
        }
    }

    /// <summary>The Recommended Option's label, or <see langword="null"/> for a matter-of-taste Tweak.</summary>
    public string? RecommendedLabel { get; }

    public bool HasRecommended => RecommendedLabel is not null;

    /// <summary>When applying or undoing this Tweak takes effect.</summary>
    public string ActivationText { get; }

    /// <summary>The Tweak takes effect only after a sign-out or restart, so its Activation is highlighted.</summary>
    public bool IsDelayed { get; }

    public bool IsImmediate => !IsDelayed;

    public bool IsCustom { get; }

    /// <summary>The Tweak is no longer in the Option Akari-Dash last applied; Re-apply is offered.</summary>
    public bool IsDrifted => _driftedFrom is not null;

    /// <summary>What Akari-Dash applied and what the machine is in now, when the Tweak has drifted.</summary>
    public string? DriftText { get; }

    /// <summary>Undo is offered only on Tweaks Akari-Dash has applied.</summary>
    public bool IsApplied { get; }

    /// <summary>Why the Tweak cannot be used on this PC, when it is Unavailable.</summary>
    public string? UnavailableReason { get; }

    /// <summary>An Unavailable Tweak is greyed out and offers no Option to pick.</summary>
    public bool IsUnavailable => UnavailableReason is not null;

    /// <summary>Greys out an Unavailable row.</summary>
    public double RowOpacity => IsUnavailable ? 0.5 : 1.0;

    /// <summary>The toggle is hidden while the live values match no Option (neither of its sides is true; the selector stands in), and for selectors.</summary>
    public bool IsToggleVisible => !IsSelector && !MatchesNoOption && !IsUnavailable;

    /// <summary>
    /// The selector is shown for three or more Options, and in place of the toggle while the live values
    /// match no Option, so a Custom Tweak can still be put into one; never for an Unavailable Tweak.
    /// </summary>
    public bool IsSelectorVisible => (IsSelector || MatchesNoOption) && !IsUnavailable;

    /// <summary>The actual live value(s) when the Tweak is Custom.</summary>
    public string? CustomValue { get; }

    /// <summary>The live values match none of the Options (Custom, or drifted to a Custom value).</summary>
    private bool MatchesNoOption { get; }

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
            ? MachineValue.Describe(custom.Values[0].Value)
            : string.Join("; ", custom.Values.Select(v => $"{v.Target.Location} = {MachineValue.Describe(v.Value)}"));
}
