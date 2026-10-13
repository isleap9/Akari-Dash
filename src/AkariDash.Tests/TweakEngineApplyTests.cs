using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class TweakEngineApplyTests
{
    private static readonly RegistryLocation First = new(RegistryHive.CurrentUser, @"Software\Akari\Test", "First");
    private static readonly RegistryLocation Second = new(RegistryHive.CurrentUser, @"Software\Akari\Test", "Second");
    private static readonly RegistryLocation Third = new(RegistryHive.LocalMachine, @"Software\Akari\Test", "Third");

    private static readonly TweakTarget FirstTarget = new(First);
    private static readonly TweakTarget SecondTarget = new(Second);
    private static readonly TweakTarget ThirdTarget = new(Third);

    private static readonly TweakOption Zero = Option("zero", 0);
    private static readonly TweakOption One = Option("one", 1);

    private static readonly DeclaredTweak Tweak = new(
        "test", "Test", "A test Tweak.", Category.Gaming, "Test Group",
        [FirstTarget, SecondTarget, ThirdTarget], [Zero, One]);

    // First existed (10), Second did not exist, Third existed (20).
    private readonly InMemoryMachine _machine = new InMemoryMachine()
        .With(First, RegistryValue.DWord(10))
        .With(Third, RegistryValue.DWord(20));

    private readonly InMemoryOriginalValuesStore _store = new();

    private static TweakOption Option(string id, int value) => new(id, id, new Dictionary<TweakTarget, MachineValue?>
    {
        [FirstTarget] = RegistryValue.DWord(value),
        [SecondTarget] = RegistryValue.DWord(value),
        [ThirdTarget] = RegistryValue.DWord(value),
    });

    private TweakEngine Engine() => new(_machine, _store);

    [Fact]
    public void Apply_writes_every_target_and_remembers_the_option()
    {
        var engine = Engine();

        engine.Apply(Tweak, Zero);

        Assert.Equal(RegistryValue.DWord(0), _machine.Read(First));
        Assert.Equal(RegistryValue.DWord(0), _machine.Read(Second));
        Assert.Equal(RegistryValue.DWord(0), _machine.Read(Third));
        Assert.Same(Zero, engine.AppliedOption(Tweak));
    }

    // A Tweak can gain a target in a later version of Akari-Dash, as privacy.activity-history gained
    // the user copy of its policy. Re-applying then writes that target, so its Original Value has to
    // be read and kept too, or Undo would leave the new value on the machine for good. The VM found
    // this: both copies of PublishUserActivities read 0 while only the machine one was recorded.
    [Fact]
    public void A_target_added_after_the_first_apply_gets_its_own_original_value()
    {
        var before = new DeclaredTweak(
            "test", "Test", "A test Tweak.", Category.Gaming, "Test Group",
            [FirstTarget, SecondTarget], [Option("zero", 0), Option("one", 1)]);
        var engine = Engine();

        engine.Apply(before, before.Options[0]);

        // The same Tweak, now also writing Third, which the machine has at 20.
        engine.Apply(Tweak, Zero);
        Assert.Equal(RegistryValue.DWord(0), _machine.Read(Third));

        engine.Undo(Tweak);

        Assert.Equal(RegistryValue.DWord(10), _machine.Read(First));
        Assert.Null(_machine.Read(Second));
        Assert.Equal(RegistryValue.DWord(20), _machine.Read(Third));
    }

    // Topping up must not disturb what was already recorded: First keeps the 10 it held before the
    // first apply, not the value the Tweak itself wrote.
    [Fact]
    public void Adding_a_target_keeps_the_original_values_already_saved()
    {
        var before = new DeclaredTweak(
            "test", "Test", "A test Tweak.", Category.Gaming, "Test Group",
            [FirstTarget], [Option("zero", 0), Option("one", 1)]);
        var engine = Engine();

        engine.Apply(before, before.Options[0]);
        engine.Apply(Tweak, One);

        var saved = _store.Get("test");

        Assert.NotNull(saved);
        Assert.Equal("one", saved.LastAppliedOptionId);
        Assert.Equal(RegistryValue.DWord(10), saved.OriginalValues.Single(original => original.Location == First).Value);
        Assert.Null(saved.OriginalValues.Single(original => original.Location == Second).Value);
        Assert.Equal(RegistryValue.DWord(20), saved.OriginalValues.Single(original => original.Location == Third).Value);
    }

    // A record saved by an older Akari-Dash covers only the targets the Tweak had then. Apply tops
    // the record up now, but a machine that applied with the old build and undid with the new one
    // would otherwise be left holding Akari-Dash's own value with nothing recorded for it, which is
    // what the VM hit on privacy.activity-history: HKLM went back, HKCU kept the 0.
    [Fact]
    public void Undo_clears_a_value_it_wrote_without_recording_it()
    {
        var engine = Engine();

        // The record an older build would have left: applied as "zero", knowing only First.
        _store.Save("test", new AppliedTweak("zero", [new OriginalValue(First, RegistryValue.DWord(10))]));

        // The newer build also writes Second and Third, as its Option says.
        _machine.Write(Second, RegistryValue.DWord(0));
        _machine.Write(Third, RegistryValue.DWord(0));

        engine.Undo(Tweak);

        Assert.Equal(RegistryValue.DWord(10), _machine.Read(First));
        Assert.Null(_machine.Read(Second));
        Assert.Null(_machine.Read(Third));
        Assert.Null(_store.Get("test"));
    }

    // Only Akari-Dash's own write is cleared. A target holding something else belongs to whoever set
    // it, so Undo leaves it alone rather than guessing.
    [Fact]
    public void Undo_leaves_an_unrecorded_value_alone_when_it_is_not_the_one_applied()
    {
        var engine = Engine();

        _store.Save("test", new AppliedTweak("zero", [new OriginalValue(First, RegistryValue.DWord(10))]));
        _machine.Write(Second, RegistryValue.DWord(7));
        _machine.Write(Third, RegistryValue.DWord(0));

        engine.Undo(Tweak);

        Assert.Equal(RegistryValue.DWord(10), _machine.Read(First));
        Assert.Equal(RegistryValue.DWord(7), _machine.Read(Second));
        Assert.Null(_machine.Read(Third));
    }

    [Fact]
    public void Tweak_never_applied_has_no_applied_option()
    {
        Assert.Null(Engine().AppliedOption(Tweak));
    }

    [Fact]
    public void Original_values_are_saved_only_before_the_first_apply()
    {
        var engine = Engine();

        engine.Apply(Tweak, Zero);
        _machine.Write(First, RegistryValue.DWord(5));
        engine.Apply(Tweak, Zero);
        engine.Undo(Tweak);

        Assert.Equal(RegistryValue.DWord(10), _machine.Read(First));
    }

    [Fact]
    public void Switching_options_keeps_the_original_values()
    {
        var engine = Engine();

        engine.Apply(Tweak, Zero);
        engine.Apply(Tweak, One);
        Assert.Same(One, engine.AppliedOption(Tweak));

        engine.Undo(Tweak);

        Assert.Equal(RegistryValue.DWord(10), _machine.Read(First));
        Assert.Equal(RegistryValue.DWord(20), _machine.Read(Third));
    }

    [Fact]
    public void Undo_deletes_values_that_did_not_exist_before_the_first_apply()
    {
        var engine = Engine();

        engine.Apply(Tweak, Zero);
        engine.Undo(Tweak);

        Assert.Null(_machine.Read(Second));
    }

    [Fact]
    public void Undo_clears_the_stored_original_values()
    {
        var engine = Engine();

        engine.Apply(Tweak, Zero);
        engine.Undo(Tweak);

        Assert.Null(engine.AppliedOption(Tweak));
        Assert.Null(_store.Get(Tweak.Id));
    }

    [Fact]
    public void Undo_of_a_tweak_never_applied_changes_nothing()
    {
        Engine().Undo(Tweak);

        Assert.Equal(RegistryValue.DWord(10), _machine.Read(First));
        Assert.Null(_machine.Read(Second));
    }

    [Fact]
    public void Original_values_survive_a_new_engine_over_the_same_store()
    {
        Engine().Apply(Tweak, Zero);

        var reopened = Engine();
        Assert.Same(Zero, reopened.AppliedOption(Tweak));

        reopened.Undo(Tweak);
        Assert.Equal(RegistryValue.DWord(10), _machine.Read(First));
    }

    [Fact]
    public void When_a_write_fails_earlier_writes_are_rolled_back_and_the_failing_target_is_named()
    {
        _machine.FailingWritesTo(Third);
        var engine = Engine();

        var error = Assert.Throws<TweakApplyException>(() => engine.Apply(Tweak, Zero));

        Assert.Equal(Third, error.FailedTarget);
        Assert.Contains(Third.ToString(), error.Message);
        Assert.Equal(RegistryValue.DWord(10), _machine.Read(First));
        Assert.Null(_machine.Read(Second));
        Assert.Equal(RegistryValue.DWord(20), _machine.Read(Third));
    }

    [Fact]
    public void A_failed_first_apply_leaves_the_tweak_not_applied()
    {
        _machine.FailingWritesTo(Third);
        var engine = Engine();

        Assert.Throws<TweakApplyException>(() => engine.Apply(Tweak, Zero));

        Assert.Null(engine.AppliedOption(Tweak));
        Assert.Null(_store.Get(Tweak.Id));
    }

    [Fact]
    public void A_failed_option_switch_rolls_back_to_the_applied_option_and_keeps_the_original_values()
    {
        var engine = Engine();
        engine.Apply(Tweak, Zero);
        _machine.FailingWritesTo(Third);

        Assert.Throws<TweakApplyException>(() => engine.Apply(Tweak, One));

        Assert.Same(Zero, engine.AppliedOption(Tweak));
        Assert.Same(Zero, Assert.IsType<LiveState.InOption>(engine.ReadLiveState(Tweak)).Option);
        Assert.Equal(
            [
                new OriginalValue(First, RegistryValue.DWord(10)),
                new OriginalValue(Second, null),
                new OriginalValue(Third, RegistryValue.DWord(20)),
            ],
            _store.Get(Tweak.Id)!.OriginalValues);
    }

    [Fact]
    public void When_rolling_back_also_fails_the_error_says_so_and_undo_stays_available()
    {
        // Second is written, Third fails, then putting Second back fails too.
        var machine = new RollbackFailingMachine(_machine, failWrite: Third, failDelete: Second);
        var engine = new TweakEngine(machine, _store);

        var error = Assert.Throws<TweakApplyException>(() => engine.Apply(Tweak, Zero));

        Assert.Equal(Third, error.FailedTarget);
        Assert.False(error.RolledBack);
        Assert.Contains(Second.ToString(), error.Message);
        Assert.Equal(RegistryValue.DWord(10), _machine.Read(First));
        Assert.NotNull(engine.AppliedOption(Tweak));
    }

    [Fact]
    public void Apply_and_undo_through_a_dry_run_write_nothing()
    {
        var dryRun = new DryRunMachine(_machine);
        var engine = new TweakEngine(_machine, _store, applyTo: dryRun);

        engine.Apply(Tweak, Zero);
        engine.Undo(Tweak);

        Assert.Equal(RegistryValue.DWord(10), _machine.Read(First));
        Assert.Null(_machine.Read(Second));
        Assert.Empty(dryRun.PlannedChanges);
    }
}
