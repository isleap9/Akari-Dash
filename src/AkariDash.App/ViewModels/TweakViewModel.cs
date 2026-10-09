using AkariDash.Core.Tweaks;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>
/// One Tweak row. A two-Option Tweak is shown as a toggle whose off side is its first Option
/// and whose on side is its second.
/// </summary>
public sealed class TweakViewModel : ViewModelBase
{
    public TweakViewModel(DeclaredTweak tweak, LiveState state)
    {
        Title = tweak.Title;
        Description = tweak.Description;
        OffLabel = tweak.Options[0].Label;
        OnLabel = tweak.Options[1].Label;
        IsOn = state is LiveState.InOption inOption && inOption.Option == tweak.Options[1];

        if (state is LiveState.Custom custom)
        {
            IsCustom = true;
            CustomValue = custom.Values.Count == 1
                ? Describe(custom.Values[0])
                : string.Join("; ", custom.Values.Select(v => $"{v.Target.Location} = {Describe(v)}"));
        }
    }

    public string Description { get; }

    public string OffLabel { get; }

    public string OnLabel { get; }

    public bool IsOn { get; }

    public bool IsCustom { get; }

    /// <summary>The toggle is hidden while Custom: neither of its sides is true.</summary>
    public bool IsToggleVisible => !IsCustom;

    /// <summary>The actual live value(s) when the Tweak is Custom.</summary>
    public string? CustomValue { get; }

    private static string Describe(TargetValue value) => value.Value?.ToString() ?? "(not set)";
}
