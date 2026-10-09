using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>Runs Tweaks against a <see cref="IMachine"/>.</summary>
public sealed class TweakEngine(IMachine machine)
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

    /// <summary>What Windows behaves as for <paramref name="value"/>, treating a missing value as the target's <see cref="RegistryTarget.AbsentMeans"/>.</summary>
    private static RegistryValue? Effective(RegistryTarget target, RegistryValue? value) =>
        value ?? target.AbsentMeans;
}
