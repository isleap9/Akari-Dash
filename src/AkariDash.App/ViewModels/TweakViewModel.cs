using System.Diagnostics;
using Microsoft.UI.Xaml;
using CommunityToolkit.Mvvm.Input;
using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>
/// One Tweak row. A two-Option Tweak is shown as a toggle whose off side is its first Option
/// and whose on side is its second; a Tweak with more Options is shown as a selector. A Tweak with a
/// <see cref="TunerLayout"/> is drawn as a tuner card instead: chips, a Current line and buttons.
/// </summary>
public sealed partial class TweakViewModel : ViewModelBase
{
    private readonly DeclaredTweak _tweak;
    private readonly Action<DeclaredTweak, TweakOption> _chooseOption;
    private readonly Action<DeclaredTweak> _undo;
    private readonly Action? _refresh;
    private readonly TweakOption? _driftedFrom;
    private bool _isOn;
    private TweakOption? _selectedOption;

    /// <param name="isApplied">Whether Akari-Dash has applied this Tweak (and so can Undo it).</param>
    /// <param name="appliedOption">
    /// The Option Akari-Dash last applied, where it has. A row that is no longer in it says what
    /// differs from it rather than listing every value it touches, which for a Tweak over dozens of
    /// them is the difference between one line and a paragraph.
    /// </param>
    /// <param name="chooseOption">Called when the user picks an Option; the row is rebuilt afterwards.</param>
    /// <param name="undo">Called when the user asks to Undo; the row is rebuilt afterwards.</param>
    /// <param name="refresh">Re-reads every row; a tuner card's Read current button calls it.</param>
    public TweakViewModel(
        DeclaredTweak tweak,
        LiveState state,
        bool isApplied,
        Action<DeclaredTweak, TweakOption> chooseOption,
        Action<DeclaredTweak> undo,
        Action? refresh = null,
        TweakOption? appliedOption = null)
    {
        _tweak = tweak;
        _chooseOption = chooseOption;
        _undo = undo;
        _refresh = refresh;
        IsApplied = isApplied;

        Title = tweak.Title;
        Description = tweak.Description;
        OffLabel = tweak.Options[0].Label;
        OnLabel = tweak.Options[1].Label;
        Options = tweak.Options;
        IsSelector = tweak.Options.Count > 2;
        RecommendedLabel = tweak.Recommended?.Label;
        if (state is LiveState.Unavailable unavailable)
        {
            UnavailableReason = unavailable.Reason;
        }

        // A drifted row shows what the machine is actually in now.
        var actual = state;
        if (state is LiveState.Drifted drifted)
        {
            _driftedFrom = drifted.Expected;
            DriftText = $"Akari-Dash set {drifted.Expected.Label}, but it is now {Describe(drifted.Actual, drifted.Expected)}.";
            actual = drifted.Actual;
        }

        _selectedOption = (actual as LiveState.InOption)?.Option;
        _isOn = _selectedOption == tweak.Options[1];

        // What the row says it takes: the Option the machine is in where that Option waits longer
        // than its Tweak, so "UAC off" reads After restart while the other levels read Immediately.
        var activation = _selectedOption is null ? tweak.Activation : tweak.ActivationOf(_selectedOption);
        ActivationText = activation switch
        {
            Activation.Immediately => "Immediately",
            Activation.AfterSignOut => "After sign-out",
            Activation.AfterRestart => "After restart",
            Activation.InSafeMode => "In safe mode, next restart",
            _ => throw new UnreachableException($"Unknown Activation: {activation}"),
        };
        IsDelayed = activation != Activation.Immediately;

        if (actual is LiveState.Custom custom)
        {
            // Drift takes precedence: a drifted Tweak shows its value in the Drift badge instead.
            IsCustom = !IsDrifted;
            MatchesNoOption = true;
            CustomValue = Describe(custom, appliedOption);
        }

        if (tweak.Tuner is { } tuner)
        {
            Chips = tuner.Chips is { } chips
                ? chips.Select(chip => new TunerChipViewModel(chip.Option, chip.Label, chip.Option == _selectedOption, Pick)).ToList()
                : tweak.Options.Select(option => new TunerChipViewModel(option, option.Label, option == _selectedOption, Pick)).ToList();

            // A card that chips only some of its Options keeps the rest in a list beside them.
            HasOtherOptions = Chips.Count < tweak.Options.Count;

            var current = actual switch
            {
                LiveState.InOption inOption => inOption.Option.Values[tweak.Targets[0]],
                LiveState.Custom customValue => customValue.Values[0].Value,
                _ => null,
            };
            TunerCurrent = $"Current: {DescribeNumber(current, tuner)}" + (tuner.Detail is { } detail ? $"   |   {detail}" : string.Empty);
        }
    }

    /// <summary>The Tweak is drawn as a tuner card instead of a row.</summary>
    public bool IsTuner => _tweak.Tuner is not null;

    /// <summary>A tuner card's Options; one is checked when the live value matches it.</summary>
    public IReadOnlyList<TunerChipViewModel> Chips { get; } = [];

    /// <summary>A tuner card's "Current: ..." line, with its detail (such as this PC's RAM).</summary>
    public string? TunerCurrent { get; }

    /// <summary>The tuner card chips only some of its Options, so the rest need a list of their own.</summary>
    public bool HasOtherOptions { get; }

    /// <summary>That list is hidden on an Unavailable Tweak, like every other way of changing it.</summary>
    public bool IsOtherOptionsVisible => HasOtherOptions && !IsUnavailable;

    /// <summary>Re-reads the live value, like AkariOS-Ultimate's Read current.</summary>
    [RelayCommand]
    private void ReadCurrent() => _refresh?.Invoke();

    /// <summary>Applies the tuner's Windows default, like AkariOS-Ultimate's Default button.</summary>
    [RelayCommand]
    private void ApplyDefault()
    {
        if (_tweak.Tuner is { } tuner)
        {
            _chooseOption(_tweak, tuner.DefaultOption);
        }
    }

    /// <summary>Applies the checked chip; it asks first, like every other change.</summary>
    [RelayCommand]
    private void ApplyChip()
    {
        if (Chips.FirstOrDefault(chip => chip.IsChecked == true) is { } chip)
        {
            _chooseOption(_tweak, chip.Option);
        }
    }

    private void Pick(TunerChipViewModel picked)
    {
        foreach (var chip in Chips.Where(chip => chip != picked))
        {
            chip.Clear();
        }
    }

    // A DWORD shown as AkariOS-Ultimate shows it: unsigned, with thousands separators, or as hex
    // with its decimal in brackets for a value that is normally written that way.
    private static string DescribeNumber(MachineValue? value, TunerLayout tuner) => value switch
    {
        RegistryValue { Data: int number } when tuner.Hex => $"0x{unchecked((uint)number):X2} ({unchecked((uint)number)})",
        RegistryValue { Data: int number } => $"{unchecked((uint)number):N0} {tuner.Unit}",
        null => "not set",
        _ => $"{MachineValue.Describe(value)} {tuner.Unit}".TrimEnd(),
    };

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

    /// <summary>
    /// Whether the search sent the user to this row. Set while the page is being built, before the
    /// rows are bound, so the row draws its outline and scrolls itself into view as it loads.
    /// </summary>
    public bool IsHighlighted { get; init; }

    /// <summary>The outline that points the row out when the search sent the user to it.</summary>
    public Thickness HighlightThickness => new(IsHighlighted ? 2 : 0);

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

    private static string Describe(LiveState state, TweakOption? against) => state switch
    {
        LiveState.InOption inOption => inOption.Option.Label,
        LiveState.Custom custom => Describe(custom, against),
        _ => throw new UnreachableException($"Drift is never in another state: {state}"),
    };

    /// <summary>
    /// What the machine holds, named against <paramref name="against"/> where there is one: only the
    /// values that differ from it, since the ones that match it say nothing the row has not said.
    /// A Tweak over one value just shows that value.
    /// </summary>
    private static string Describe(LiveState.Custom custom, TweakOption? against)
    {
        if (custom.Values.Count == 1)
        {
            return MachineValue.Describe(custom.Values[0].Value);
        }

        var shown = against is null
            ? custom.Values
            : custom.Values
                .Where(v => v.Target.Effective(v.Value) != v.Target.Effective(against.Values[v.Target]))
                .ToList();

        return shown.Count == 0
            ? MachineValue.Describe(null)
            : string.Join("; ", shown.Select(v => $"{v.Target.Location} = {MachineValue.Describe(v.Value)}"));
    }
}
