using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class TweakEngineRecommendedTests
{
    private static readonly RegistryLocation GameModeValue = Location("GameMode");
    private static readonly RegistryLocation PowerValue = Location("Power");
    private static readonly RegistryLocation TasteValue = Location("Taste");
    private static readonly RegistryLocation FirstOfTwo = Location("FirstOfTwo");
    private static readonly RegistryLocation SecondOfTwo = Location("SecondOfTwo");

    private static readonly DeclaredTweak GameMode = Recommends("game-mode", GameModeValue);
    private static readonly DeclaredTweak Power = Recommends("power", PowerValue);
    private static readonly DeclaredTweak Taste = Recommends("taste", TasteValue) with { Recommended = null };
    private static readonly DeclaredTweak NvidiaOnly = Recommends("nvidia", Location("Nvidia")) with { RequiresGpu = GpuVendor.Nvidia };
    private static readonly DeclaredTweak TwoTargets = Recommends("two-targets", FirstOfTwo, SecondOfTwo);

    // Every value starts at 0; every Tweak recommends 1.
    private readonly InMemoryMachine _machine = new InMemoryMachine()
        .With(GameModeValue, RegistryValue.DWord(0))
        .With(PowerValue, RegistryValue.DWord(0))
        .With(TasteValue, RegistryValue.DWord(0))
        .With(FirstOfTwo, RegistryValue.DWord(0))
        .With(SecondOfTwo, RegistryValue.DWord(0));

    private static RegistryLocation Location(string name) => new(RegistryHive.CurrentUser, @"Software\Akari\Test", name);

    /// <summary>A Tweak with Options "off" (0) and "on" (1) on every target, recommending "on".</summary>
    private static DeclaredTweak Recommends(string id, params RegistryLocation[] locations)
    {
        var targets = locations.Select(location => new TweakTarget(location)).ToList();
        var off = Option("off", targets, 0);
        var on = Option("on", targets, 1);
        return new DeclaredTweak(id, id, "A test Tweak.", Category.Gaming, "Test Group", targets, [off, on], Recommended: on);
    }

    private static TweakOption Option(string id, IReadOnlyList<TweakTarget> targets, int value) =>
        new(id, id, targets.ToDictionary(target => target, MachineValue? (_) => RegistryValue.DWord(value)));

    private TweakEngine Engine() => new(_machine);

    [Fact]
    public void The_plan_lists_every_tweak_to_put_into_its_recommended_option_with_what_will_change()
    {
        var plan = Engine().PlanRecommended([GameMode, Power]);

        Assert.Equal([GameMode, Power], plan.ToApply.Select(planned => planned.Tweak));
        Assert.Equal([GameMode.Recommended, Power.Recommended], plan.ToApply.Select(planned => planned.Option));
        Assert.Equal(
            [new PlannedChange(GameModeValue, RegistryValue.DWord(0), RegistryValue.DWord(1))],
            plan.ToApply[0].Changes);
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void Unavailable_tweaks_are_skipped_with_the_reason()
    {
        var plan = Engine().PlanRecommended([NvidiaOnly, GameMode]);

        var skipped = Assert.Single(plan.Skipped);
        Assert.Same(NvidiaOnly, skipped.Tweak);
        Assert.Equal("Needs an NVIDIA graphics card; none was found on this PC.", skipped.Reason);
        Assert.Equal([GameMode], plan.ToApply.Select(planned => planned.Tweak));
    }

    [Fact]
    public void Tweaks_without_a_recommended_option_are_skipped_as_a_matter_of_taste()
    {
        var plan = Engine().PlanRecommended([Taste]);

        var skipped = Assert.Single(plan.Skipped);
        Assert.Same(Taste, skipped.Tweak);
        Assert.Equal("No Recommended Option; this one is a matter of taste.", skipped.Reason);
        Assert.Empty(plan.ToApply);
    }

    [Fact]
    public void Tweaks_already_in_their_recommended_option_are_skipped_as_nothing_would_change()
    {
        _machine.With(PowerValue, RegistryValue.DWord(1));

        var plan = Engine().PlanRecommended([Power]);

        var skipped = Assert.Single(plan.Skipped);
        Assert.Equal("Already on.", skipped.Reason);
        Assert.Empty(plan.ToApply);
    }

    [Fact]
    public void A_tweak_whose_values_cannot_be_read_is_skipped_with_the_error_while_the_others_are_planned()
    {
        var engine = new TweakEngine(new ReadFailingMachine(_machine, PowerValue));

        var plan = engine.PlanRecommended([Power, GameMode]);

        var skipped = Assert.Single(plan.Skipped);
        Assert.Same(Power, skipped.Tweak);
        Assert.Equal("Could not read its current values: Access is denied.", skipped.Reason);
        Assert.Equal([GameMode], plan.ToApply.Select(planned => planned.Tweak));
    }

    [Fact]
    public void Applying_the_plan_puts_every_tweak_into_its_recommended_option_and_reports_each()
    {
        var engine = Engine();

        var results = engine.ApplyRecommended(engine.PlanRecommended([GameMode, Power]));

        Assert.Equal([GameMode, Power], results.Select(result => result.Tweak));
        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(RegistryValue.DWord(1), _machine.Read(GameModeValue));
        Assert.Equal(RegistryValue.DWord(1), _machine.Read(PowerValue));
    }

    [Fact]
    public void A_failing_tweak_is_rolled_back_and_reported_while_the_others_still_apply()
    {
        var machine = new RollbackFailingMachine(_machine, failWrite: SecondOfTwo, failDelete: Location("Unused"));
        var engine = new TweakEngine(machine);
        var plan = engine.PlanRecommended([GameMode, TwoTargets, Power]);

        var results = engine.ApplyRecommended(plan);

        Assert.Equal([true, false, true], results.Select(result => result.Succeeded));
        Assert.IsType<TweakApplyException>(results[1].Error);
        Assert.Equal(RegistryValue.DWord(1), _machine.Read(GameModeValue));
        Assert.Equal(RegistryValue.DWord(0), _machine.Read(FirstOfTwo));
        Assert.Equal(RegistryValue.DWord(1), _machine.Read(PowerValue));
        Assert.False(engine.IsApplied(TwoTargets));
    }

    [Fact]
    public void A_tweak_that_became_unavailable_since_the_plan_is_reported_as_failed_without_stopping_the_others()
    {
        var service = new TweakTarget(new ServiceLocation("AkariTestService"));
        var disabled = new TweakOption("off", "Off", new Dictionary<TweakTarget, MachineValue?> { [service] = new ServiceStartValue(ServiceStartType.Disabled) });
        var serviceTweak = new DeclaredTweak("service", "Service", "A test Tweak.", Category.Gaming, "Test Group", [service], [disabled], Recommended: disabled);
        var plan = new RecommendedPlan([new PlannedTweak(serviceTweak, disabled, []), new PlannedTweak(GameMode, GameMode.Recommended!, [])], []);

        var results = Engine().ApplyRecommended(plan);

        Assert.IsType<TweakUnavailableException>(results[0].Error);
        Assert.True(results[1].Succeeded);
    }

    [Fact]
    public void In_a_dry_run_every_tweak_reports_success_and_nothing_is_written()
    {
        var engine = new TweakEngine(_machine, applyTo: new DryRunMachine(_machine));

        var results = engine.ApplyRecommended(engine.PlanRecommended([GameMode, Power]));

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(RegistryValue.DWord(0), _machine.Read(GameModeValue));
        Assert.Equal(RegistryValue.DWord(0), _machine.Read(PowerValue));
    }
}

/// <summary>Wraps a machine so that reading <c>failRead</c> fails, as access denied would.</summary>
internal sealed class ReadFailingMachine(IMachine inner, MachineLocation failRead) : IMachine
{
    public MachineValue? Read(MachineLocation location) =>
        location == failRead ? throw new UnauthorizedAccessException("Access is denied.") : inner.Read(location);

    public void Write(MachineLocation location, MachineValue value) => inner.Write(location, value);

    public void Delete(MachineLocation location) => inner.Delete(location);

    public IReadOnlySet<GpuVendor> GpuVendors() => inner.GpuVendors();

    public void CreateRestorePoint(string description) => inner.CreateRestorePoint(description);
}
