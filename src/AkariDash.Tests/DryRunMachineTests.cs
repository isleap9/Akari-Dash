using AkariDash.Core.Machine;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class DryRunMachineTests
{
    private static readonly RegistryLocation Existing = new(RegistryHive.CurrentUser, @"Software\Akari\Test", "Existing");
    private static readonly RegistryLocation Missing = new(RegistryHive.CurrentUser, @"Software\Akari\Test", "Missing");

    private readonly InMemoryMachine _inner = new InMemoryMachine().With(Existing, RegistryValue.DWord(10));

    [Fact]
    public void Reads_pass_through_to_the_wrapped_machine()
    {
        var dryRun = new DryRunMachine(_inner);

        Assert.Equal(RegistryValue.DWord(10), dryRun.Read(Existing));
        Assert.Null(dryRun.Read(Missing));
    }

    [Fact]
    public void Overwriting_a_value_is_recorded_and_not_performed()
    {
        var dryRun = new DryRunMachine(_inner);

        dryRun.Write(Existing, RegistryValue.DWord(0));

        Assert.Equal([new PlannedChange(Existing, RegistryValue.DWord(10), RegistryValue.DWord(0))], dryRun.PlannedChanges);
        Assert.Equal(RegistryValue.DWord(10), _inner.Read(Existing));
    }

    [Fact]
    public void Creating_a_value_is_recorded_as_a_create_and_not_performed()
    {
        var dryRun = new DryRunMachine(_inner);

        dryRun.Write(Missing, RegistryValue.DWord(1));

        var change = Assert.Single(dryRun.PlannedChanges);
        Assert.True(change.IsCreate);
        Assert.Equal(RegistryValue.DWord(1), change.After);
        Assert.Null(_inner.Read(Missing));
    }

    [Fact]
    public void Deleting_a_value_is_recorded_as_a_delete_and_not_performed()
    {
        var dryRun = new DryRunMachine(_inner);

        dryRun.Delete(Existing);

        var change = Assert.Single(dryRun.PlannedChanges);
        Assert.True(change.IsDelete);
        Assert.Equal(RegistryValue.DWord(10), change.Before);
        Assert.Equal(RegistryValue.DWord(10), _inner.Read(Existing));
    }

    [Fact]
    public void Later_reads_see_earlier_recorded_writes_and_deletes()
    {
        var dryRun = new DryRunMachine(_inner);

        dryRun.Write(Missing, RegistryValue.DWord(1));
        dryRun.Delete(Existing);

        Assert.Equal(RegistryValue.DWord(1), dryRun.Read(Missing));
        Assert.Null(dryRun.Read(Existing));
    }

    [Fact]
    public void Writing_the_value_already_there_records_nothing()
    {
        var dryRun = new DryRunMachine(_inner);

        dryRun.Write(Existing, RegistryValue.DWord(10));
        dryRun.Delete(Missing);

        Assert.Empty(dryRun.PlannedChanges);
    }

    [Fact]
    public void Repeated_writes_to_one_value_keep_the_original_before_and_the_latest_after()
    {
        var dryRun = new DryRunMachine(_inner);

        dryRun.Write(Existing, RegistryValue.DWord(0));
        dryRun.Write(Existing, RegistryValue.DWord(5));

        Assert.Equal([new PlannedChange(Existing, RegistryValue.DWord(10), RegistryValue.DWord(5))], dryRun.PlannedChanges);
    }

    [Fact]
    public void Before_is_what_the_machine_held_at_the_first_write()
    {
        var dryRun = new DryRunMachine(_inner);

        dryRun.Write(Existing, RegistryValue.DWord(0));
        _inner.Write(Existing, RegistryValue.DWord(7));

        Assert.Equal(RegistryValue.DWord(10), Assert.Single(dryRun.PlannedChanges).Before);
    }

    [Fact]
    public void Writing_a_value_back_to_where_it_started_drops_its_change()
    {
        var dryRun = new DryRunMachine(_inner);

        dryRun.Write(Existing, RegistryValue.DWord(0));
        dryRun.Write(Existing, RegistryValue.DWord(10));

        Assert.Empty(dryRun.PlannedChanges);
    }

    [Fact]
    public void Planned_changes_describe_before_and_after_and_call_out_creates_and_deletes()
    {
        Assert.Equal(
            @"HKCU\Software\Akari\Test\Existing: 10 → 0",
            new PlannedChange(Existing, RegistryValue.DWord(10), RegistryValue.DWord(0)).ToString());
        Assert.Equal(
            @"HKCU\Software\Akari\Test\Missing: (not set) → 1 (created)",
            new PlannedChange(Missing, null, RegistryValue.DWord(1)).ToString());
        Assert.Equal(
            @"HKCU\Software\Akari\Test\Existing: 10 → (not set) (deleted)",
            new PlannedChange(Existing, RegistryValue.DWord(10), null).ToString());
    }
}
