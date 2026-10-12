using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

/// <summary>One name=value; entry of DirectXUserGlobalSettings as a Tweak target (issue #28).</summary>
public class TweakEngineEntryTests
{
    private static readonly RegistryLocation Settings = new(RegistryHive.CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings");
    private static readonly RegistryEntryLocation Vrr = new(Settings.Hive, Settings.Key, Settings.Name, "VRROptimizeEnable");
    private static readonly TweakTarget Target = new(Vrr);

    private static readonly DeclaredTweak Tweak = new(
        "vrr", "VRR", "An entry.", Category.Gaming, "Graphics", [Target],
        [
            new TweakOption("on", "On", new Dictionary<TweakTarget, MachineValue?> { [Target] = new EntryValue("1") }),
            new TweakOption("off", "Off", new Dictionary<TweakTarget, MachineValue?> { [Target] = new EntryValue("0") }),
            new TweakOption("windows-decides", "Let Windows decide", new Dictionary<TweakTarget, MachineValue?> { [Target] = null }),
        ]);

    private readonly InMemoryOriginalValuesStore _store = new();

    private static InMemoryMachine With(string settings) => new InMemoryMachine().With(Settings, RegistryValue.String(settings));

    [Fact]
    public void Apply_changes_only_its_entry_and_keeps_the_others_in_order()
    {
        var machine = With("SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;AutoHDREnable=1;");

        new TweakEngine(machine, _store).Apply(Tweak, Tweak.Options[0]);

        Assert.Equal(RegistryValue.String("SwapEffectUpgradeEnable=1;VRROptimizeEnable=1;AutoHDREnable=1;"), machine.Read(Settings));
    }

    [Fact]
    public void A_missing_entry_is_added_and_undo_removes_it_again()
    {
        var machine = With("SwapEffectUpgradeEnable=1;");
        var engine = new TweakEngine(machine, _store);

        engine.Apply(Tweak, Tweak.Options[1]);
        Assert.Equal(RegistryValue.String("SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;"), machine.Read(Settings));

        engine.Undo(Tweak);
        Assert.Equal(RegistryValue.String("SwapEffectUpgradeEnable=1;"), machine.Read(Settings));
    }

    [Fact]
    public void Removing_the_last_entry_deletes_the_value_and_a_missing_value_reads_as_windows_decides()
    {
        var machine = new InMemoryMachine();
        var engine = new TweakEngine(machine, _store);

        Assert.Equal("windows-decides", Assert.IsType<LiveState.InOption>(engine.ReadLiveState(Tweak)).Option.Id);

        engine.Apply(Tweak, Tweak.Options[0]);
        engine.Undo(Tweak);

        Assert.Null(machine.Read(Settings));
    }

    [Fact]
    public void Entries_round_trip_through_the_original_values_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), "EntryTests." + Guid.NewGuid().ToString("N"));
        try
        {
            new FileOriginalValuesStore(folder).Save("vrr", new AppliedTweak("off", [new OriginalValue(Vrr, new EntryValue("1"))]));

            Assert.Equal(new OriginalValue(Vrr, new EntryValue("1")), Assert.Single(new FileOriginalValuesStore(folder).Get("vrr")!.OriginalValues));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
