using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class TweakEngineMultiOptionTests
{
    private static readonly RegistryLocation Location =
        new(RegistryHive.LocalMachine, @"Software\Akari\Test", "Value");

    private static readonly TweakTarget Target = new(Location);

    private static readonly TweakOption Low = Option("low", 0);
    private static readonly TweakOption Medium = Option("medium", 1);
    private static readonly TweakOption High = Option("high", 2);

    private static readonly DeclaredTweak Tweak =
        new("test", "Test", "A three-Option Tweak.", Category.Gaming, "Test Group", [Target], [Low, Medium, High]);

    private static TweakOption Option(string id, int value) =>
        new(id, id, new Dictionary<TweakTarget, MachineValue?> { [Target] = RegistryValue.DWord(value) });

    private static TweakEngine Engine(int value) =>
        new(new InMemoryMachine().With(Location, RegistryValue.DWord(value)));

    [Fact]
    public void Resolves_to_whichever_of_three_options_matches()
    {
        Assert.Same(Low, Assert.IsType<LiveState.InOption>(Engine(0).ReadLiveState(Tweak)).Option);
        Assert.Same(Medium, Assert.IsType<LiveState.InOption>(Engine(1).ReadLiveState(Tweak)).Option);
        Assert.Same(High, Assert.IsType<LiveState.InOption>(Engine(2).ReadLiveState(Tweak)).Option);
    }

    [Fact]
    public void Value_matching_none_of_three_options_is_custom()
    {
        var custom = Assert.IsType<LiveState.Custom>(Engine(7).ReadLiveState(Tweak));

        Assert.Equal(RegistryValue.DWord(7), Assert.Single(custom.Values).Value);
    }

    [Fact]
    public void Preview_lists_the_change_to_any_option()
    {
        var change = Assert.Single(Engine(0).Preview(Tweak, High));

        Assert.Equal(new PlannedChange(Location, RegistryValue.DWord(0), RegistryValue.DWord(2)), change);
    }

    [Fact]
    public void Apply_moves_between_any_two_options()
    {
        var machine = new InMemoryMachine().With(Location, RegistryValue.DWord(0));
        var engine = new TweakEngine(machine);

        engine.Apply(Tweak, High);
        engine.Apply(Tweak, Medium);

        Assert.Same(Medium, Assert.IsType<LiveState.InOption>(engine.ReadLiveState(Tweak)).Option);
    }
}
