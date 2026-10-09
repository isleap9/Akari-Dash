using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// Runs Tweaks against a <see cref="IMachine"/>. Live State and previews always read
/// <paramref name="machine"/>; <see cref="Apply"/> writes to <paramref name="applyTo"/>
/// (a <see cref="DryRunMachine"/> in Phase 1), or to <paramref name="machine"/> when not given.
/// </summary>
public sealed class TweakEngine(IMachine machine, IMachine? applyTo = null)
{
    /// <summary>Reads which Option <paramref name="tweak"/> is in right now.</summary>
    public LiveState ReadLiveState(DeclaredTweak tweak)
    {
        var live = tweak.Targets
            .Select(target => new TargetValue(target, machine.ReadRegistryValue(target.Location)))
            .ToList();

        var option = tweak.Options.FirstOrDefault(option =>
            live.All(value => Effective(value.Target, value.Value) == Effective(value.Target, option.Values[value.Target])));

        return option is null ? new LiveState.Custom(live) : new LiveState.InOption(option);
    }

    /// <summary>Lists what applying <paramref name="option"/> would change, writing nothing.</summary>
    public IReadOnlyList<PlannedChange> Preview(DeclaredTweak tweak, TweakOption option)
    {
        var dryRun = new DryRunMachine(machine);
        Write(dryRun, tweak, option);
        return dryRun.PlannedChanges;
    }

    /// <summary>Puts every target of <paramref name="tweak"/> into <paramref name="option"/>.</summary>
    public void Apply(DeclaredTweak tweak, TweakOption option) => Write(applyTo ?? machine, tweak, option);

    private static void Write(IMachine target, DeclaredTweak tweak, TweakOption option)
    {
        foreach (var registryTarget in tweak.Targets)
        {
            if (option.Values[registryTarget] is { } value)
            {
                target.WriteRegistryValue(registryTarget.Location, value);
            }
            else
            {
                target.DeleteRegistryValue(registryTarget.Location);
            }
        }
    }

    /// <summary>What Windows behaves as for <paramref name="value"/>, treating a missing value as the target's <see cref="RegistryTarget.AbsentMeans"/>.</summary>
    private static RegistryValue? Effective(RegistryTarget target, RegistryValue? value) =>
        value ?? target.AbsentMeans;
}
