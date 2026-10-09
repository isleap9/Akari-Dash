using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>
/// One Tweak row. A two-Option Tweak is shown as a toggle whose off side is its first Option
/// and whose on side is its second.
/// </summary>
public sealed class TweakViewModel : ViewModelBase
{
    private readonly DeclaredTweak _tweak;
    private readonly Action<DeclaredTweak, TweakOption> _chooseOption;
    private bool _isOn;

    /// <param name="chooseOption">Called when the user picks an Option; the row is rebuilt afterwards.</param>
    public TweakViewModel(DeclaredTweak tweak, LiveState state, Action<DeclaredTweak, TweakOption> chooseOption)
    {
        _tweak = tweak;
        _chooseOption = chooseOption;

        Title = tweak.Title;
        Description = tweak.Description;
        OffLabel = tweak.Options[0].Label;
        OnLabel = tweak.Options[1].Label;
        _isOn = state is LiveState.InOption inOption && inOption.Option == tweak.Options[1];

        if (state is LiveState.Custom custom)
        {
            IsCustom = true;
            CustomValue = custom.Values.Count == 1
                ? RegistryValue.Describe(custom.Values[0].Value)
                : string.Join("; ", custom.Values.Select(v => $"{v.Target.Location} = {RegistryValue.Describe(v.Value)}"));
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

    /// <summary>The toggle is hidden while Custom: neither of its sides is true.</summary>
    public bool IsToggleVisible => !IsCustom;

    /// <summary>The actual live value(s) when the Tweak is Custom.</summary>
    public string? CustomValue { get; }
}
