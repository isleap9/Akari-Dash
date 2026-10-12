using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

/// <summary>A key Windows does not let an administrator read (such as WTDS\Components).</summary>
public class TweakEngineUnreadableTests
{
    private static readonly RegistryLocation Protected = new(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components", "ServiceEnabled");
    private static readonly TweakTarget Target = new(Protected);

    private static readonly DeclaredTweak Tweak = new(
        "protected", "Protected", "A Tweak on a key only SYSTEM may read.", Category.Security, "SmartScreen", [Target],
        [
            new TweakOption("off", "Off", new Dictionary<TweakTarget, MachineValue?> { [Target] = RegistryValue.DWord(0) }),
            new TweakOption("on", "On", new Dictionary<TweakTarget, MachineValue?> { [Target] = RegistryValue.DWord(1) }),
        ]);

    [Fact]
    public void An_unreadable_key_makes_the_tweak_unavailable_and_names_the_key()
    {
        var engine = new TweakEngine(new InMemoryMachine().FailingReadsOf(Protected));

        var unavailable = Assert.IsType<LiveState.Unavailable>(engine.ReadLiveState(Tweak));

        Assert.Contains(@"WTDS\Components", unavailable.Reason);
    }
}
