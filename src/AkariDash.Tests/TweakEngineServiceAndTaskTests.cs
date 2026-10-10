using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class TweakEngineServiceAndTaskTests
{
    private static readonly RegistryLocation Registry = new(RegistryHive.LocalMachine, @"Software\Akari\Test", "Value");
    private static readonly ServiceLocation Service = new("AkariTestService");
    private static readonly ScheduledTaskLocation Task = new(@"\Akari\TestTask");

    private static readonly TweakTarget RegistryTarget = new(Registry);
    private static readonly TweakTarget ServiceTarget = new(Service);
    private static readonly TweakTarget TaskTarget = new(Task);

    private static readonly ServiceStartValue Automatic = new(ServiceStartType.Automatic);
    private static readonly ServiceStartValue Disabled = new(ServiceStartType.Disabled);

    private static readonly TweakOption ServiceOff = Option("off", (ServiceTarget, Disabled));
    private static readonly TweakOption ServiceOn = Option("on", (ServiceTarget, Automatic));
    private static readonly DeclaredTweak ServiceTweak = Tweak([ServiceTarget], ServiceOff, ServiceOn);

    private static readonly TweakOption TaskOff = Option("off", (TaskTarget, TaskEnabledValue.Off));
    private static readonly TweakOption TaskOn = Option("on", (TaskTarget, TaskEnabledValue.On));
    private static readonly DeclaredTweak TaskTweak = Tweak([TaskTarget], TaskOff, TaskOn);

    // Registry first, so it is written before the service and has to be rolled back.
    private static readonly TweakOption MixedOff = Option("off", (RegistryTarget, RegistryValue.DWord(0)), (ServiceTarget, Disabled));
    private static readonly TweakOption MixedOn = Option("on", (RegistryTarget, RegistryValue.DWord(1)), (ServiceTarget, Automatic));
    private static readonly DeclaredTweak MixedTweak = Tweak([RegistryTarget, ServiceTarget], MixedOff, MixedOn);

    private readonly InMemoryMachine _machine = new InMemoryMachine()
        .With(Registry, RegistryValue.DWord(1))
        .With(Service, Automatic)
        .With(Task, TaskEnabledValue.On);

    private readonly InMemoryOriginalValuesStore _store = new();

    private static TweakOption Option(string id, params (TweakTarget Target, MachineValue Value)[] values) =>
        new(id, id, values.ToDictionary(value => value.Target, MachineValue? (value) => value.Value));

    private static DeclaredTweak Tweak(TweakTarget[] targets, params TweakOption[] options) =>
        new("test", "Test", "A test Tweak.", Category.Gaming, "Test Group", targets, options);

    private TweakEngine Engine() => new(_machine, _store);

    [Fact]
    public void Live_state_resolves_a_service_start_type_and_a_task_enabled_state()
    {
        Assert.Same(ServiceOn, Assert.IsType<LiveState.InOption>(Engine().ReadLiveState(ServiceTweak)).Option);
        Assert.Same(TaskOn, Assert.IsType<LiveState.InOption>(Engine().ReadLiveState(TaskTweak)).Option);
    }

    [Fact]
    public void Service_start_type_matching_no_option_is_custom_with_the_actual_start_type()
    {
        _machine.Write(Service, new ServiceStartValue(ServiceStartType.Manual));

        var custom = Assert.IsType<LiveState.Custom>(Engine().ReadLiveState(ServiceTweak));

        Assert.Equal(new ServiceStartValue(ServiceStartType.Manual), Assert.Single(custom.Values).Value);
    }

    [Fact]
    public void Preview_describes_service_and_task_changes()
    {
        var engine = Engine();

        Assert.Equal(
            "Service AkariTestService start type: Automatic → Disabled",
            Assert.Single(engine.Preview(ServiceTweak, ServiceOff)).ToString());
        Assert.Equal(
            @"Scheduled task \Akari\TestTask: Enabled → Disabled",
            Assert.Single(engine.Preview(TaskTweak, TaskOff)).ToString());
        Assert.Equal(new ServiceStartValue(ServiceStartType.Automatic), _machine.Read(Service));
    }

    [Fact]
    public void Apply_and_undo_a_service_start_type()
    {
        var engine = Engine();

        engine.Apply(ServiceTweak, ServiceOff);
        Assert.Equal(Disabled, _machine.Read(Service));
        Assert.Same(ServiceOff, Assert.IsType<LiveState.InOption>(engine.ReadLiveState(ServiceTweak)).Option);

        engine.Undo(ServiceTweak);
        Assert.Equal(Automatic, _machine.Read(Service));
    }

    [Fact]
    public void Undo_restores_a_delayed_start_original_value()
    {
        _machine.Write(Service, new ServiceStartValue(ServiceStartType.AutomaticDelayed));
        var engine = Engine();

        engine.Apply(ServiceTweak, ServiceOff);
        engine.Undo(ServiceTweak);

        Assert.Equal(new ServiceStartValue(ServiceStartType.AutomaticDelayed), _machine.Read(Service));
    }

    [Fact]
    public void Apply_and_undo_a_scheduled_task()
    {
        var engine = Engine();

        engine.Apply(TaskTweak, TaskOff);
        Assert.Equal(TaskEnabledValue.Off, _machine.Read(Task));
        Assert.Same(TaskOff, Assert.IsType<LiveState.InOption>(engine.ReadLiveState(TaskTweak)).Option);

        engine.Undo(TaskTweak);
        Assert.Equal(TaskEnabledValue.On, _machine.Read(Task));
    }

    [Fact]
    public void A_tweak_mixing_registry_and_service_targets_rolls_back_fully_when_the_service_write_fails()
    {
        _machine.FailingWritesTo(Service);
        var engine = Engine();

        var error = Assert.Throws<TweakApplyException>(() => engine.Apply(MixedTweak, MixedOff));

        Assert.Equal(Service, error.FailedTarget);
        Assert.True(error.RolledBack);
        Assert.Equal(RegistryValue.DWord(1), _machine.Read(Registry));
        Assert.Equal(Automatic, _machine.Read(Service));
        Assert.False(engine.IsApplied(MixedTweak));
    }

    [Fact]
    public void A_tweak_mixing_registry_and_service_targets_applies_and_undoes_both()
    {
        var engine = Engine();

        engine.Apply(MixedTweak, MixedOff);
        Assert.Equal(RegistryValue.DWord(0), _machine.Read(Registry));
        Assert.Equal(Disabled, _machine.Read(Service));

        engine.Undo(MixedTweak);
        Assert.Equal(RegistryValue.DWord(1), _machine.Read(Registry));
        Assert.Equal(Automatic, _machine.Read(Service));
    }

    [Fact]
    public void Applying_to_a_service_that_does_not_exist_fails_and_rolls_back_the_registry_target()
    {
        var machine = new InMemoryMachine().With(Registry, RegistryValue.DWord(1));
        var engine = new TweakEngine(machine, _store);

        var error = Assert.Throws<TweakApplyException>(() => engine.Apply(MixedTweak, MixedOff));

        Assert.Equal(Service, error.FailedTarget);
        Assert.True(error.RolledBack);
        Assert.Equal(RegistryValue.DWord(1), machine.Read(Registry));
        Assert.Null(machine.Read(Service));
        Assert.False(engine.IsApplied(MixedTweak));
    }
}
