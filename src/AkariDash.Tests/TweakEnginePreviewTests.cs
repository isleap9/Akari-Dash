using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class TweakEnginePreviewTests
{
    private static readonly RegistryLocation Changed = new(RegistryHive.CurrentUser, @"Software\Akari\Test", "Changed");
    private static readonly RegistryLocation Created = new(RegistryHive.LocalMachine, @"Software\Akari\Test", "Created");
    private static readonly RegistryLocation Deleted = new(RegistryHive.LocalMachine, @"Software\Akari\Test", "Deleted");

    private static readonly TweakTarget ChangedTarget = new(Changed);
    private static readonly TweakTarget CreatedTarget = new(Created);
    private static readonly TweakTarget DeletedTarget = new(Deleted);

    // "before" matches the seeded machine; "after" changes one value, creates one and deletes one.
    private static readonly TweakOption Before = new("before", "Before", new Dictionary<TweakTarget, MachineValue?>
    {
        [ChangedTarget] = RegistryValue.DWord(10),
        [CreatedTarget] = null,
        [DeletedTarget] = RegistryValue.String("old"),
    });

    private static readonly TweakOption After = new("after", "After", new Dictionary<TweakTarget, MachineValue?>
    {
        [ChangedTarget] = RegistryValue.DWord(0),
        [CreatedTarget] = RegistryValue.DWord(1),
        [DeletedTarget] = null,
    });

    private static readonly DeclaredTweak Tweak = new(
        "test", "Test", "A test Tweak.", Category.Gaming, "Test Group",
        [ChangedTarget, CreatedTarget, DeletedTarget], [Before, After]);

    private readonly InMemoryMachine _machine = new InMemoryMachine()
        .With(Changed, RegistryValue.DWord(10))
        .With(Deleted, RegistryValue.String("old"));

    [Fact]
    public void Preview_lists_each_change_create_and_delete()
    {
        var changes = new TweakEngine(_machine).Preview(Tweak, After);

        Assert.Equal(
            [
                new PlannedChange(Changed, RegistryValue.DWord(10), RegistryValue.DWord(0)),
                new PlannedChange(Created, null, RegistryValue.DWord(1)),
                new PlannedChange(Deleted, RegistryValue.String("old"), null),
            ],
            changes);
    }

    [Fact]
    public void Preview_writes_nothing()
    {
        new TweakEngine(_machine).Preview(Tweak, After);

        Assert.Equal(RegistryValue.DWord(10), _machine.Read(Changed));
        Assert.Null(_machine.Read(Created));
        Assert.Equal(RegistryValue.String("old"), _machine.Read(Deleted));
    }

    [Fact]
    public void Preview_of_the_option_the_machine_is_already_in_is_empty()
    {
        Assert.Empty(new TweakEngine(_machine).Preview(Tweak, Before));
    }

    [Fact]
    public void Apply_puts_every_target_into_the_option()
    {
        var engine = new TweakEngine(_machine);

        engine.Apply(Tweak, After);

        Assert.Equal(RegistryValue.DWord(0), _machine.Read(Changed));
        Assert.Equal(RegistryValue.DWord(1), _machine.Read(Created));
        Assert.Null(_machine.Read(Deleted));
        Assert.Same(After, Assert.IsType<LiveState.InOption>(engine.ReadLiveState(Tweak)).Option);
    }

    [Fact]
    public void Apply_through_a_dry_run_records_the_change_and_writes_nothing()
    {
        var dryRun = new DryRunMachine(_machine);
        var engine = new TweakEngine(_machine, applyTo: dryRun);

        engine.Apply(Tweak, After);

        Assert.Equal(3, dryRun.PlannedChanges.Count);
        Assert.Same(Before, Assert.IsType<LiveState.InOption>(engine.ReadLiveState(Tweak)).Option);
        Assert.Equal(RegistryValue.DWord(10), _machine.Read(Changed));
        Assert.Null(_machine.Read(Created));
        Assert.Equal(RegistryValue.String("old"), _machine.Read(Deleted));
    }
}
