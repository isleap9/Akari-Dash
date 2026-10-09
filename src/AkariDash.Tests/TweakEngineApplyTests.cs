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

    private static readonly RegistryTarget FirstTarget = new(First);
    private static readonly RegistryTarget SecondTarget = new(Second);
    private static readonly RegistryTarget ThirdTarget = new(Third);

    private static readonly TweakOption Zero = Option("zero", 0);
    private static readonly TweakOption One = Option("one", 1);

    private static readonly DeclaredTweak Tweak = new(
        "test", "Test", "A test Tweak.", Category.Gaming, "Test Group",
        [FirstTarget, SecondTarget, ThirdTarget], [Zero, One]);

    // First existed (10), Second did not exist, Third existed (20).
    private readonly InMemoryMachine _machine = new InMemoryMachine()
        .WithRegistryValue(First, RegistryValue.DWord(10))
        .WithRegistryValue(Third, RegistryValue.DWord(20));

    private readonly InMemoryOriginalValuesStore _store = new();

    private static TweakOption Option(string id, int value) => new(id, id, new Dictionary<RegistryTarget, RegistryValue?>
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

        Assert.Equal(RegistryValue.DWord(0), _machine.ReadRegistryValue(First));
        Assert.Equal(RegistryValue.DWord(0), _machine.ReadRegistryValue(Second));
        Assert.Equal(RegistryValue.DWord(0), _machine.ReadRegistryValue(Third));
        Assert.Same(Zero, engine.AppliedOption(Tweak));
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
        _machine.WriteRegistryValue(First, RegistryValue.DWord(5));
        engine.Apply(Tweak, Zero);
        engine.Undo(Tweak);

        Assert.Equal(RegistryValue.DWord(10), _machine.ReadRegistryValue(First));
    }

    [Fact]
    public void Switching_options_keeps_the_original_values()
    {
        var engine = Engine();

        engine.Apply(Tweak, Zero);
        engine.Apply(Tweak, One);
        Assert.Same(One, engine.AppliedOption(Tweak));

        engine.Undo(Tweak);

        Assert.Equal(RegistryValue.DWord(10), _machine.ReadRegistryValue(First));
        Assert.Equal(RegistryValue.DWord(20), _machine.ReadRegistryValue(Third));
    }

    [Fact]
    public void Undo_deletes_values_that_did_not_exist_before_the_first_apply()
    {
        var engine = Engine();

        engine.Apply(Tweak, Zero);
        engine.Undo(Tweak);

        Assert.Null(_machine.ReadRegistryValue(Second));
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

        Assert.Equal(RegistryValue.DWord(10), _machine.ReadRegistryValue(First));
        Assert.Null(_machine.ReadRegistryValue(Second));
    }

    [Fact]
    public void Original_values_survive_a_new_engine_over_the_same_store()
    {
        Engine().Apply(Tweak, Zero);

        var reopened = Engine();
        Assert.Same(Zero, reopened.AppliedOption(Tweak));

        reopened.Undo(Tweak);
        Assert.Equal(RegistryValue.DWord(10), _machine.ReadRegistryValue(First));
    }

    [Fact]
    public void When_a_write_fails_earlier_writes_are_rolled_back_and_the_failing_target_is_named()
    {
        _machine.FailingWritesTo(Third);
        var engine = Engine();

        var error = Assert.Throws<TweakApplyException>(() => engine.Apply(Tweak, Zero));

        Assert.Equal(Third, error.FailedTarget);
        Assert.Contains(Third.ToString(), error.Message);
        Assert.Equal(RegistryValue.DWord(10), _machine.ReadRegistryValue(First));
        Assert.Null(_machine.ReadRegistryValue(Second));
        Assert.Equal(RegistryValue.DWord(20), _machine.ReadRegistryValue(Third));
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
        Assert.Equal(RegistryValue.DWord(10), _machine.ReadRegistryValue(First));
        Assert.NotNull(engine.AppliedOption(Tweak));
    }

    [Fact]
    public void Apply_and_undo_through_a_dry_run_write_nothing()
    {
        var dryRun = new DryRunMachine(_machine);
        var engine = new TweakEngine(_machine, _store, applyTo: dryRun);

        engine.Apply(Tweak, Zero);
        engine.Undo(Tweak);

        Assert.Equal(RegistryValue.DWord(10), _machine.ReadRegistryValue(First));
        Assert.Null(_machine.ReadRegistryValue(Second));
        Assert.Empty(dryRun.PlannedChanges);
    }
}
