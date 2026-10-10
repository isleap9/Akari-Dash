using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class TweakEngineRestorePointTests
{
    private static readonly RegistryLocation FirstValue = Location("First");
    private static readonly RegistryLocation SecondValue = Location("Second");
    private static readonly DeclaredTweak First = Tweak("first", FirstValue);
    private static readonly DeclaredTweak Second = Tweak("second", SecondValue);

    private readonly InMemoryMachine _machine = new();

    private static RegistryLocation Location(string name) => new(RegistryHive.CurrentUser, @"Software\Akari\Test", name);

    /// <summary>A Tweak with Options "off" (0) and "on" (1), recommending "on".</summary>
    private static DeclaredTweak Tweak(string id, RegistryLocation location)
    {
        var target = new TweakTarget(location);
        var off = new TweakOption("off", "off", new Dictionary<TweakTarget, MachineValue?> { [target] = RegistryValue.DWord(0) });
        var on = new TweakOption("on", "on", new Dictionary<TweakTarget, MachineValue?> { [target] = RegistryValue.DWord(1) });
        return new DeclaredTweak(id, id, "A test Tweak.", Category.Gaming, "Test Group", [target], [off, on], Recommended: on);
    }

    [Fact]
    public void Nothing_is_requested_before_the_first_apply()
    {
        var engine = new TweakEngine(_machine);

        engine.ReadLiveState(First);
        engine.Preview(First, First.Options[1]);
        engine.PlanRecommended([First, Second]);

        Assert.Empty(_machine.RestorePointsRequested);
    }

    [Fact]
    public void The_first_apply_requests_one_restore_point_before_writing()
    {
        var machine = new WriteCheckingMachine(_machine, FirstValue);
        var engine = new TweakEngine(machine);

        engine.Apply(First, First.Options[1]);

        Assert.Single(_machine.RestorePointsRequested);
        Assert.False(machine.WrittenBeforeRestorePoint);
    }

    [Fact]
    public void Later_applies_switches_and_re_applies_in_the_same_session_request_no_more()
    {
        var engine = new TweakEngine(_machine);

        engine.Apply(First, First.Options[1]);
        engine.Apply(First, First.Options[0]);
        engine.Apply(Second, Second.Options[1]);
        _machine.Write(SecondValue, RegistryValue.DWord(0));
        engine.Apply(Second, Second.Options[1]);

        Assert.Single(_machine.RestorePointsRequested);
    }

    [Fact]
    public void Apply_all_recommended_requests_one_restore_point_for_the_whole_run()
    {
        var engine = new TweakEngine(_machine);

        engine.ApplyRecommended(engine.PlanRecommended([First, Second]));

        Assert.Single(_machine.RestorePointsRequested);
        Assert.Equal(RegistryValue.DWord(1), _machine.Read(SecondValue));
    }

    [Fact]
    public void A_new_session_requests_its_own_restore_point()
    {
        var store = new InMemoryOriginalValuesStore();
        new TweakEngine(_machine, store).Apply(First, First.Options[1]);

        new TweakEngine(_machine, store).Apply(First, First.Options[0]);

        Assert.Equal(2, _machine.RestorePointsRequested.Count);
    }

    [Fact]
    public void Dry_runs_request_no_restore_point()
    {
        var engine = new TweakEngine(_machine, applyTo: new DryRunMachine(_machine));

        engine.Apply(First, First.Options[1]);
        engine.ApplyRecommended(engine.PlanRecommended([First, Second]));

        Assert.Empty(_machine.RestorePointsRequested);
    }

    [Fact]
    public void Applying_an_unavailable_tweak_requests_no_restore_point()
    {
        var engine = new TweakEngine(_machine);
        var nvidiaOnly = First with { RequiresGpu = GpuVendor.Nvidia };

        Assert.Throws<TweakUnavailableException>(() => engine.Apply(nvidiaOnly, nvidiaOnly.Options[1]));

        Assert.Empty(_machine.RestorePointsRequested);
    }

    [Fact]
    public void Undo_requests_no_restore_point()
    {
        var store = new InMemoryOriginalValuesStore();
        new TweakEngine(new InMemoryMachine(), store).Apply(First, First.Options[1]);

        new TweakEngine(_machine, store).Undo(First);

        Assert.Empty(_machine.RestorePointsRequested);
    }

    [Fact]
    public void A_failed_restore_point_does_not_block_the_apply_and_is_reported_once()
    {
        _machine.FailingRestorePoints();
        var engine = new TweakEngine(_machine);

        engine.Apply(First, First.Options[1]);

        Assert.Equal(RegistryValue.DWord(1), _machine.Read(FirstValue));
        Assert.Equal("System Restore is turned off.", engine.TakeRestorePointFailure()?.Message);
        Assert.Null(engine.TakeRestorePointFailure());
    }

    [Fact]
    public void A_failed_restore_point_is_not_retried_in_the_same_session()
    {
        _machine.FailingRestorePoints();
        var engine = new TweakEngine(_machine);

        engine.Apply(First, First.Options[1]);
        engine.Apply(Second, Second.Options[1]);

        Assert.Single(_machine.RestorePointsRequested);
    }

    [Fact]
    public void A_created_restore_point_reports_no_failure()
    {
        var engine = new TweakEngine(_machine);

        engine.Apply(First, First.Options[1]);

        Assert.Null(engine.TakeRestorePointFailure());
    }

    [Fact]
    public void A_restore_point_is_due_only_until_the_first_real_apply()
    {
        var engine = new TweakEngine(_machine);
        Assert.True(engine.RestorePointDue);

        engine.Apply(First, First.Options[1]);

        Assert.False(engine.RestorePointDue);
    }

    [Fact]
    public void A_restore_point_is_never_due_for_dry_runs()
    {
        Assert.False(new TweakEngine(_machine, applyTo: new DryRunMachine(_machine)).RestorePointDue);
    }

    [Fact]
    public async Task An_apply_started_while_the_restore_point_is_being_created_waits_for_it()
    {
        var machine = new SlowRestorePointMachine(_machine);
        var engine = new TweakEngine(machine);

        var first = Task.Run(() => engine.Apply(First, First.Options[1]));
        Assert.True(machine.Creating.Wait(TimeSpan.FromSeconds(10)));
        var second = Task.Run(() => engine.Apply(Second, Second.Options[1]));

        await Task.Delay(100);
        Assert.Null(_machine.Read(SecondValue));

        machine.Finish.Set();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(RegistryValue.DWord(1), _machine.Read(SecondValue));
        Assert.Single(_machine.RestorePointsRequested);
    }

    /// <summary>Holds every restore point in creation until <see cref="Finish"/> is set.</summary>
    private sealed class SlowRestorePointMachine(InMemoryMachine inner) : IMachine
    {
        public ManualResetEventSlim Creating { get; } = new();

        public ManualResetEventSlim Finish { get; } = new();

        public MachineValue? Read(MachineLocation location) => inner.Read(location);

        public void Write(MachineLocation location, MachineValue value) => inner.Write(location, value);

        public void Delete(MachineLocation location) => inner.Delete(location);

        public IReadOnlySet<GpuVendor> GpuVendors() => inner.GpuVendors();

        public void CreateRestorePoint(string description)
        {
            inner.CreateRestorePoint(description);
            Creating.Set();
            Finish.Wait(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>Notes whether <c>watched</c> is written before any restore point has been asked for.</summary>
    private sealed class WriteCheckingMachine(InMemoryMachine inner, MachineLocation watched) : IMachine
    {
        public bool WrittenBeforeRestorePoint { get; private set; }

        public MachineValue? Read(MachineLocation location) => inner.Read(location);

        public void Write(MachineLocation location, MachineValue value)
        {
            WrittenBeforeRestorePoint |= location == watched && inner.RestorePointsRequested.Count == 0;
            inner.Write(location, value);
        }

        public void Delete(MachineLocation location) => inner.Delete(location);

        public IReadOnlySet<GpuVendor> GpuVendors() => inner.GpuVendors();

        public void CreateRestorePoint(string description) => inner.CreateRestorePoint(description);
    }
}
