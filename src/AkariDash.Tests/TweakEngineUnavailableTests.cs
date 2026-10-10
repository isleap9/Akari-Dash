using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class TweakEngineUnavailableTests
{
    private static readonly RegistryLocation Registry = new(RegistryHive.LocalMachine, @"Software\Akari\Test", "Value");
    private static readonly ServiceLocation Service = new("AkariTestService");
    private static readonly ScheduledTaskLocation Task = new(@"\Akari\TestTask");

    private static readonly TweakTarget RegistryTarget = new(Registry);
    private static readonly TweakTarget ServiceTarget = new(Service);
    private static readonly TweakTarget TaskTarget = new(Task);

    private static readonly ServiceStartValue Automatic = new(ServiceStartType.Automatic);
    private static readonly ServiceStartValue Disabled = new(ServiceStartType.Disabled);

    private static readonly TweakOption RegistryOff = Option("off", (RegistryTarget, RegistryValue.DWord(0)));
    private static readonly TweakOption RegistryOn = Option("on", (RegistryTarget, RegistryValue.DWord(1)));

    private static readonly DeclaredTweak ServiceTweak = Tweak(
        [ServiceTarget], Option("off", (ServiceTarget, Disabled)), Option("on", (ServiceTarget, Automatic)));

    private static readonly DeclaredTweak TaskTweak = Tweak(
        [TaskTarget], Option("off", (TaskTarget, TaskEnabledValue.Off)), Option("on", (TaskTarget, TaskEnabledValue.On)));

    private static readonly DeclaredTweak NvidiaTweak = Tweak([RegistryTarget], RegistryOff, RegistryOn) with
    {
        RequiresGpu = GpuVendor.Nvidia,
    };

    private readonly InMemoryOriginalValuesStore _store = new();

    private static TweakOption Option(string id, params (TweakTarget Target, MachineValue Value)[] values) =>
        new(id, id, values.ToDictionary(value => value.Target, MachineValue? (value) => value.Value));

    private static DeclaredTweak Tweak(TweakTarget[] targets, params TweakOption[] options) =>
        new("test", "Test", "A test Tweak.", Category.Gaming, "Test Group", targets, options);

    [Fact]
    public void A_tweak_whose_service_is_not_installed_is_unavailable_with_the_reason()
    {
        var engine = new TweakEngine(new InMemoryMachine(), _store);

        var unavailable = Assert.IsType<LiveState.Unavailable>(engine.ReadLiveState(ServiceTweak));

        Assert.Equal("The AkariTestService service is not installed on this PC.", unavailable.Reason);
    }

    [Fact]
    public void A_tweak_whose_scheduled_task_does_not_exist_is_unavailable_with_the_reason()
    {
        var engine = new TweakEngine(new InMemoryMachine(), _store);

        var unavailable = Assert.IsType<LiveState.Unavailable>(engine.ReadLiveState(TaskTweak));

        Assert.Equal(@"The scheduled task \Akari\TestTask does not exist on this PC.", unavailable.Reason);
    }

    [Fact]
    public void A_missing_registry_value_does_not_make_a_tweak_unavailable()
    {
        var engine = new TweakEngine(new InMemoryMachine().WithGpu(GpuVendor.Nvidia), _store);

        Assert.IsNotType<LiveState.Unavailable>(engine.ReadLiveState(NvidiaTweak));
    }

    [Fact]
    public void A_tweak_gated_on_a_gpu_vendor_is_unavailable_on_other_hardware_with_the_reason()
    {
        var machine = new InMemoryMachine().With(Registry, RegistryValue.DWord(1)).WithGpu(GpuVendor.Amd);
        var engine = new TweakEngine(machine, _store);

        var unavailable = Assert.IsType<LiveState.Unavailable>(engine.ReadLiveState(NvidiaTweak));

        Assert.Equal("Needs an NVIDIA graphics card; this PC has AMD.", unavailable.Reason);
    }

    [Fact]
    public void A_tweak_gated_on_a_gpu_vendor_is_unavailable_when_no_graphics_card_is_found()
    {
        var engine = new TweakEngine(new InMemoryMachine().With(Registry, RegistryValue.DWord(1)), _store);

        var unavailable = Assert.IsType<LiveState.Unavailable>(engine.ReadLiveState(NvidiaTweak));

        Assert.Equal("Needs an NVIDIA graphics card; none was found on this PC.", unavailable.Reason);
    }

    [Fact]
    public void A_tweak_gated_on_a_gpu_vendor_is_available_when_any_graphics_card_matches()
    {
        var machine = new InMemoryMachine()
            .With(Registry, RegistryValue.DWord(1))
            .WithGpu(GpuVendor.Intel)
            .WithGpu(GpuVendor.Nvidia);
        var engine = new TweakEngine(machine, _store);

        Assert.Same(RegistryOn, Assert.IsType<LiveState.InOption>(engine.ReadLiveState(NvidiaTweak)).Option);
    }

    [Fact]
    public void An_unavailable_tweak_cannot_be_previewed()
    {
        var engine = new TweakEngine(new InMemoryMachine(), _store);

        var error = Assert.Throws<TweakUnavailableException>(() => engine.Preview(ServiceTweak, ServiceTweak.Options[0]));

        Assert.Equal("The AkariTestService service is not installed on this PC.", error.Reason);
    }

    [Fact]
    public void An_unavailable_tweak_cannot_be_applied_and_nothing_is_written_or_saved()
    {
        var machine = new InMemoryMachine().With(Registry, RegistryValue.DWord(1)).WithGpu(GpuVendor.Amd);
        var engine = new TweakEngine(machine, _store);

        Assert.Throws<TweakUnavailableException>(() => engine.Apply(NvidiaTweak, RegistryOff));

        Assert.Equal(RegistryValue.DWord(1), machine.Read(Registry));
        Assert.False(engine.IsApplied(NvidiaTweak));
    }

    [Fact]
    public void Availability_is_read_from_the_real_machine_when_applies_go_to_a_dry_run()
    {
        var machine = new InMemoryMachine().WithGpu(GpuVendor.Amd);
        var engine = new TweakEngine(machine, _store, applyTo: new DryRunMachine(machine));

        Assert.IsType<LiveState.Unavailable>(engine.ReadLiveState(NvidiaTweak));
        Assert.Throws<TweakUnavailableException>(() => engine.Apply(NvidiaTweak, RegistryOff));
    }
}
