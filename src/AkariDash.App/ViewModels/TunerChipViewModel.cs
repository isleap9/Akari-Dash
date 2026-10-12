using AkariDash.Core.Tweaks;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>One Option of a tuner card, drawn as a chip; checking it only selects it until Apply.</summary>
public sealed class TunerChipViewModel(TweakOption option, bool isChecked, Action<TunerChipViewModel> picked) : ViewModelBase
{
    private bool? _isChecked = isChecked;

    public TweakOption Option { get; } = option;

    public string Label => Option.Label;

    /// <summary>Nullable for ToggleButton; a chip is never indeterminate.</summary>
    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            // A chip cannot be unchecked by clicking it again; another chip has to be picked.
            if (value != true)
            {
                OnPropertyChanged();
                return;
            }

            if (SetProperty(ref _isChecked, true))
            {
                picked(this);
            }
        }
    }

    /// <summary>Unchecks this chip because another one was picked.</summary>
    internal void Clear()
    {
        if (_isChecked == true)
        {
            _isChecked = false;
            OnPropertyChanged(nameof(IsChecked));
        }
    }
}
