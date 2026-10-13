using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class TweakEngineDriftTests
{
    private static readonly RegistryLocation Location =
        new(RegistryHive.CurrentUser, @"Software\Akari\Test", "Value");

    private static readonly TweakTarget Target = new(Location);

    private static readonly TweakOption Off = Option("off", 0);
    private static readonly TweakOption On = Option("on", 1);

    private static readonly DeclaredTweak Tweak =
        new("test", "Test", "A test Tweak.", Category.Gaming, "Test Group", [Target], [Off, On]);

    // The machine's own value before Akari-Dash touches it.
    private readonly InMemoryMachine _machine = new InMemoryMachine().With(Location, RegistryValue.DWord(1));

    private readonly InMemoryOriginalValuesStore _store = new();

    private static TweakOption Option(string id, int value) =>
        new(id, id, new Dictionary<TweakTarget, MachineValue?> { [Target] = RegistryValue.DWord(value) });

    private TweakEngine Engine() => new(_machine, _store);

    [Fact]
    public void Tweak_reset_to_another_option_after_apply_has_drifted()
    {
        var engine = Engine();
        engine.Apply(Tweak, Off);

        _machine.Write(Location, RegistryValue.DWord(1));

        var drifted = Assert.IsType<LiveState.Drifted>(engine.ReadLiveState(Tweak));
        Assert.Same(Off, drifted.Expected);
        Assert.Same(On, Assert.IsType<LiveState.InOption>(drifted.Actual).Option);
    }

    [Fact]
    public void Drift_wins_over_custom_when_the_drifted_value_matches_no_option()
    {
        var engine = Engine();
        engine.Apply(Tweak, Off);

        _machine.Write(Location, RegistryValue.DWord(5));

        var drifted = Assert.IsType<LiveState.Drifted>(engine.ReadLiveState(Tweak));
        Assert.Same(Off, drifted.Expected);
        var custom = Assert.IsType<LiveState.Custom>(drifted.Actual);
        Assert.Equal(RegistryValue.DWord(5), Assert.Single(custom.Values).Value);
    }

    [Fact]
    public void Tweak_never_applied_does_not_drift()
    {
        _machine.Write(Location, RegistryValue.DWord(5));

        Assert.IsType<LiveState.Custom>(Engine().ReadLiveState(Tweak));
    }

    [Fact]
    public void Tweak_still_in_the_applied_option_has_not_drifted()
    {
        var engine = Engine();
        engine.Apply(Tweak, Off);

        Assert.Same(Off, Assert.IsType<LiveState.InOption>(engine.ReadLiveState(Tweak)).Option);
    }

    [Fact]
    public void Tweak_undone_after_drifting_no_longer_drifts()
    {
        var engine = Engine();
        engine.Apply(Tweak, Off);
        _machine.Write(Location, RegistryValue.DWord(5));

        engine.Undo(Tweak);

        Assert.Same(On, Assert.IsType<LiveState.InOption>(engine.ReadLiveState(Tweak)).Option);
    }

    [Fact]
    public void Reapplying_returns_to_the_applied_option_and_keeps_the_original_values()
    {
        var engine = Engine();
        engine.Apply(Tweak, Off);
        _machine.Write(Location, RegistryValue.DWord(5));

        engine.Apply(Tweak, Assert.IsType<LiveState.Drifted>(engine.ReadLiveState(Tweak)).Expected);

        Assert.Same(Off, Assert.IsType<LiveState.InOption>(engine.ReadLiveState(Tweak)).Option);
        Assert.Equal(
            [new OriginalValue(Location, RegistryValue.DWord(1))],
            _store.Get(Tweak.Id)!.OriginalValues);
    }

    [Fact]
    public void Dry_run_apply_does_not_show_as_drift()
    {
        // Phase 1: Live State reads the real machine, which a Dry Run apply never changes.
        var engine = new TweakEngine(_machine, _store, applyTo: new DryRunMachine(_machine));

        engine.Apply(Tweak, Off);

        Assert.Same(On, Assert.IsType<LiveState.InOption>(engine.ReadLiveState(Tweak)).Option);
    }

    // A second Tweak over the same value, as an update policy mode sits over the rows beneath it.
    private static readonly DeclaredTweak Covering =
        new("covering", "Covering", "Writes the same value.", Category.Gaming, "Test Group", [Target], [Off, On]);

    [Fact]
    public void A_value_another_applied_tweak_changed_is_not_drift()
    {
        // The #31 case: the update policy mode is applied, then one of the rows it covers is set to
        // something else. The mode has not drifted — Akari-Dash moved it, on the user's instruction —
        // and offering to re-apply would undo the choice they just made.
        var engine = Engine();
        engine.Apply(Covering, Off);
        engine.Apply(Tweak, On);

        Assert.Same(On, Assert.IsType<LiveState.InOption>(engine.ReadLiveState(Covering, [Tweak, Covering])).Option);
        Assert.Same(On, Assert.IsType<LiveState.InOption>(engine.ReadLiveState(Tweak, [Tweak, Covering])).Option);
    }

    [Fact]
    public void Windows_resetting_the_value_is_still_drift_even_beside_another_applied_tweak()
    {
        var engine = Engine();
        engine.Apply(Covering, Off);
        engine.Apply(Tweak, Off);

        // Nothing Akari-Dash applied asks for 5, so this is Windows, and it is Drift for both.
        _machine.Write(Location, RegistryValue.DWord(5));

        Assert.IsType<LiveState.Drifted>(engine.ReadLiveState(Covering, [Tweak, Covering]));
        Assert.IsType<LiveState.Drifted>(engine.ReadLiveState(Tweak, [Tweak, Covering]));
    }

    [Fact]
    public void An_unapplied_tweak_over_the_same_value_explains_nothing()
    {
        var engine = Engine();
        engine.Apply(Covering, Off);

        // Covering's value moved, and the only other Tweak that holds it was never applied, so
        // nothing accounts for the move but Windows.
        _machine.Write(Location, RegistryValue.DWord(1));

        Assert.IsType<LiveState.Drifted>(engine.ReadLiveState(Covering, [Tweak, Covering]));
    }

    [Fact]
    public void Without_a_catalog_every_move_is_still_drift()
    {
        // The default stays as it was: a caller that does not say what else may write these values
        // gets the old reading, so nothing silently stops reporting drift.
        var engine = Engine();
        engine.Apply(Covering, Off);
        engine.Apply(Tweak, On);

        Assert.IsType<LiveState.Drifted>(engine.ReadLiveState(Covering));
    }
}
