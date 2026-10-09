using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class TweakEngineLiveStateTests
{
    private static readonly RegistryLocation Location =
        new(RegistryHive.CurrentUser, @"Software\Akari\Test", "Value");

    private static readonly RegistryTarget Target = new(Location);

    private static readonly TweakOption Off = Option("off", RegistryValue.DWord(0));
    private static readonly TweakOption On = Option("on", RegistryValue.DWord(1));

    private static TweakOption Option(string id, RegistryValue? value) =>
        new(id, id, new Dictionary<RegistryTarget, RegistryValue?> { [Target] = value });

    private static DeclaredTweak Tweak(params TweakOption[] options) =>
        Tweak(Target, options);

    private static DeclaredTweak Tweak(RegistryTarget target, params TweakOption[] options) =>
        new("test", "Test", "A test Tweak.", Category.Gaming, "Test Group", [target], options);

    private static LiveState Read(InMemoryMachine machine, DeclaredTweak tweak) =>
        new TweakEngine(machine).ReadLiveState(tweak);

    [Fact]
    public void Resolves_to_the_option_whose_values_match_the_machine()
    {
        var tweak = Tweak(Off, On);

        var offState = Read(new InMemoryMachine().WithRegistryValue(Location, RegistryValue.DWord(0)), tweak);
        var onState = Read(new InMemoryMachine().WithRegistryValue(Location, RegistryValue.DWord(1)), tweak);

        Assert.Same(Off, Assert.IsType<LiveState.InOption>(offState).Option);
        Assert.Same(On, Assert.IsType<LiveState.InOption>(onState).Option);
    }

    [Fact]
    public void Value_matching_no_option_is_custom_with_the_actual_value()
    {
        var machine = new InMemoryMachine().WithRegistryValue(Location, RegistryValue.DWord(5));

        var state = Read(machine, Tweak(Off, On));

        var custom = Assert.IsType<LiveState.Custom>(state);
        var value = Assert.Single(custom.Values);
        Assert.Equal(Target, value.Target);
        Assert.Equal(RegistryValue.DWord(5), value.Value);
    }

    [Fact]
    public void Value_of_a_different_kind_is_custom()
    {
        var machine = new InMemoryMachine().WithRegistryValue(Location, RegistryValue.String("1"));

        var state = Read(machine, Tweak(Off, On));

        Assert.IsType<LiveState.Custom>(state);
    }

    [Fact]
    public void Missing_value_resolves_to_the_option_where_it_does_not_exist()
    {
        var absent = Option("absent", null);

        var state = Read(new InMemoryMachine(), Tweak(absent, On));

        Assert.Same(absent, Assert.IsType<LiveState.InOption>(state).Option);
    }

    [Fact]
    public void Missing_value_resolves_to_the_option_matching_what_absent_means()
    {
        var target = new RegistryTarget(Location, AbsentMeans: RegistryValue.DWord(1));
        var off = new TweakOption("off", "Off", new Dictionary<RegistryTarget, RegistryValue?> { [target] = RegistryValue.DWord(0) });
        var on = new TweakOption("on", "On", new Dictionary<RegistryTarget, RegistryValue?> { [target] = RegistryValue.DWord(1) });

        var state = Read(new InMemoryMachine(), Tweak(target, off, on));

        Assert.Same(on, Assert.IsType<LiveState.InOption>(state).Option);
    }

    [Fact]
    public void Missing_value_matching_no_option_is_custom_with_no_value()
    {
        var state = Read(new InMemoryMachine(), Tweak(Off, On));

        var custom = Assert.IsType<LiveState.Custom>(state);
        Assert.Null(Assert.Single(custom.Values).Value);
    }

    [Fact]
    public void Tweak_with_several_targets_is_in_an_option_only_when_every_target_matches()
    {
        var second = new RegistryTarget(new RegistryLocation(RegistryHive.LocalMachine, @"Software\Akari\Test", "Other"));
        var off = new TweakOption("off", "Off", new Dictionary<RegistryTarget, RegistryValue?>
        {
            [Target] = RegistryValue.DWord(0),
            [second] = RegistryValue.DWord(0),
        });
        var on = new TweakOption("on", "On", new Dictionary<RegistryTarget, RegistryValue?>
        {
            [Target] = RegistryValue.DWord(1),
            [second] = RegistryValue.DWord(1),
        });
        var tweak = new DeclaredTweak("multi", "Multi", "Two targets.", Category.Gaming, "Test Group", [Target, second], [off, on]);
        var machine = new InMemoryMachine()
            .WithRegistryValue(Target.Location, RegistryValue.DWord(1))
            .WithRegistryValue(second.Location, RegistryValue.DWord(0));

        var state = Read(machine, tweak);

        var custom = Assert.IsType<LiveState.Custom>(state);
        Assert.Equal(
            [new TargetValue(Target, RegistryValue.DWord(1)), new TargetValue(second, RegistryValue.DWord(0))],
            custom.Values);
    }
}
