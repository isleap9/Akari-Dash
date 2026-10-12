using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Xunit;

namespace AkariDash.Tests;

/// <summary>Boot settings of the current Windows entry as Tweak targets (issue #26).</summary>
public class TweakEngineBootSettingTests
{
    private static readonly BootSettingLocation Hypervisor = new("hypervisorlaunchtype");
    private static readonly BootSettingLocation Isolated = new("isolatedcontext");

    private static readonly TweakTarget HypervisorTarget = new(Hypervisor);
    private static readonly TweakTarget IsolatedTarget = new(Isolated);

    private static readonly DeclaredTweak Tweak = new(
        "boot", "Boot", "Boot settings.", Category.Security, "Core isolation", [HypervisorTarget, IsolatedTarget],
        [
            new TweakOption("off", "Off", new Dictionary<TweakTarget, MachineValue?> { [HypervisorTarget] = null, [IsolatedTarget] = null }),
            new TweakOption("auto", "Auto", new Dictionary<TweakTarget, MachineValue?> { [HypervisorTarget] = new BootSettingValue("Auto"), [IsolatedTarget] = null }),
        ]);

    private readonly InMemoryMachine _machine = new InMemoryMachine()
        .With(Hypervisor, new BootSettingValue("Auto"))
        .With(Isolated, new BootSettingValue("Yes"));

    private readonly InMemoryOriginalValuesStore _store = new();

    [Fact]
    public void A_missing_boot_setting_is_normal_not_unavailable()
    {
        var engine = new TweakEngine(new InMemoryMachine(), _store);

        Assert.Equal("off", Assert.IsType<LiveState.InOption>(engine.ReadLiveState(Tweak)).Option.Id);
    }

    [Fact]
    public void Off_deletes_the_settings_and_undo_puts_back_exactly_what_was_there()
    {
        var engine = new TweakEngine(_machine, _store);

        engine.Apply(Tweak, Tweak.Options[0]);
        Assert.Null(_machine.Read(Hypervisor));
        Assert.Null(_machine.Read(Isolated));

        engine.Undo(Tweak);
        Assert.Equal(new BootSettingValue("Auto"), _machine.Read(Hypervisor));
        Assert.Equal(new BootSettingValue("Yes"), _machine.Read(Isolated));
    }

    [Fact]
    public void Boot_settings_round_trip_through_the_original_values_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), "BootSettingTests." + Guid.NewGuid().ToString("N"));
        try
        {
            new FileOriginalValuesStore(folder).Save("boot", new AppliedTweak("off",
                [new OriginalValue(Hypervisor, new BootSettingValue("Auto")), new OriginalValue(Isolated, null)]));

            var originals = new FileOriginalValuesStore(folder).Get("boot")!.OriginalValues;

            Assert.Equal(new OriginalValue(Hypervisor, new BootSettingValue("Auto")), originals[0]);
            Assert.Equal(new OriginalValue(Isolated, null), originals[1]);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
