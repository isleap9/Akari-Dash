using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkariDash.Core.Gpus;
using AkariDash.Core.Machine;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>
/// One row of Vain Toolbox's Registry Settings: the value's name, Vain's recommended value (ticked when
/// the card holds it), the value as it is now to edit, and three buttons - write the recommended value,
/// write what was typed, delete the value so the driver uses its own default.
/// </summary>
public sealed partial class NvidiaRegistryRowViewModel : ViewModelBase
{
    private readonly IMachine _machine;
    private readonly RegistryLocation _location;
    private readonly Action<string> _failed;

    public NvidiaRegistryRowViewModel(NvidiaRegistrySetting setting, string cardKey, IMachine machine, Action<string> failed)
    {
        Setting = setting;
        _machine = machine;
        _location = setting.Location(cardKey);
        _failed = failed;
        Refresh();
    }

    public NvidiaRegistrySetting Setting { get; }

    public string Name => Setting.Name;

    public string RecommendedText => NvidiaRegistrySettings.FormatHex(Setting.Recommended);

    /// <summary>The value in the box: what the card holds, or empty when the value is not set.</summary>
    [ObservableProperty]
    public partial string Typed { get; set; } = string.Empty;

    /// <summary>The card holds Vain's recommended value.</summary>
    [ObservableProperty]
    public partial bool IsRecommended { get; private set; }

    /// <summary>The value is set at all, so there is something to delete.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial bool IsSet { get; private set; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    /// <summary>The tick beside the recommended value: bright when the card holds it, faint when not.</summary>
    public double CheckOpacity(bool recommended) => recommended ? 1 : 0.25;

    public void Refresh()
    {
        uint? current = _machine.Read(_location) is RegistryValue { Kind: Microsoft.Win32.RegistryValueKind.DWord, Data: int data }
            ? unchecked((uint)data)
            : null;
        Typed = current is { } value ? NvidiaRegistrySettings.FormatHex(value) : string.Empty;
        IsSet = current is not null;
        IsRecommended = current == Setting.Recommended;
    }

    [RelayCommand]
    public void ApplyRecommended() => Write(Setting.Recommended);

    [RelayCommand]
    private void ApplyTyped()
    {
        if (NvidiaRegistrySettings.ParseHex(Typed) is { } value)
        {
            Write(value);
        }
        else
        {
            _failed($"{Name}: \"{Typed}\" is not a hex value of up to eight digits.");
            Refresh();
        }
    }

    [RelayCommand(CanExecute = nameof(IsSet))]
    public void Delete()
    {
        if (BuildInfo.IsDryRunOnly)
        {
            _failed($"Dry Run: {Name} was not deleted.");
            return;
        }

        try
        {
            _machine.Delete(_location);
        }
        catch (Exception ex)
        {
            _failed($"{Name} could not be deleted: {ex.Message}");
        }

        Refresh();
    }

    private void Write(uint value)
    {
        if (BuildInfo.IsDryRunOnly)
        {
            _failed($"Dry Run: {Name} was not written.");
            return;
        }

        try
        {
            _machine.Write(_location, RegistryValue.DWord(unchecked((int)value)));
        }
        catch (Exception ex)
        {
            _failed($"{Name} could not be written: {ex.Message}");
        }

        Refresh();
    }
}

/// <summary>One of Vain's Registry Settings expanders, with Apply all recommended and Delete all.</summary>
public sealed partial class NvidiaRegistryGroupViewModel(string name, IReadOnlyList<NvidiaRegistryRowViewModel> rows) : ViewModelBase
{
    public string Name { get; } = name;

    public IReadOnlyList<NvidiaRegistryRowViewModel> Rows { get; } = rows;

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    [RelayCommand]
    private void ApplyAllRecommended()
    {
        foreach (var row in Rows)
        {
            row.ApplyRecommended();
        }
    }

    [RelayCommand]
    private void DeleteAll()
    {
        foreach (var row in Rows.Where(row => row.IsSet))
        {
            row.Delete();
        }
    }
}
