using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// Runs Tweaks against a <see cref="IMachine"/>. Live State and previews always read
/// <paramref name="machine"/>; <see cref="Apply"/> and <see cref="Undo"/> write to
/// <paramref name="applyTo"/> (a <see cref="DryRunMachine"/> in Phase 1), or to
/// <paramref name="machine"/> when not given. Original Values are kept in
/// <paramref name="store"/> (this session only when not given).
/// </summary>
public sealed class TweakEngine(IMachine machine, IOriginalValuesStore? store = null, IMachine? applyTo = null)
{
    private readonly IOriginalValuesStore _store = store ?? new InMemoryOriginalValuesStore();
    private readonly IMachine _writes = applyTo ?? machine;

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
        WriteAll(dryRun, tweak, ValuesOf(tweak, option));
        return dryRun.PlannedChanges;
    }

    /// <summary>Whether Akari-Dash holds Original Values for <paramref name="tweak"/>, and so can Undo it.</summary>
    public bool IsApplied(DeclaredTweak tweak) => _store.Get(tweak.Id) is not null;

    /// <summary>The Option Akari-Dash last applied to <paramref name="tweak"/>, or <see langword="null"/> when it has not applied it.</summary>
    public TweakOption? AppliedOption(DeclaredTweak tweak) =>
        _store.Get(tweak.Id) is { } applied
            ? tweak.Options.FirstOrDefault(option => option.Id == applied.LastAppliedOptionId)
            : null;

    /// <summary>
    /// Puts every target of <paramref name="tweak"/> into <paramref name="option"/> as one unit,
    /// saving its Original Values first if none are stored yet.
    /// </summary>
    /// <exception cref="TweakApplyException">A write failed; every target already written was rolled back.</exception>
    public void Apply(DeclaredTweak tweak, TweakOption option)
    {
        var applied = _store.Get(tweak.Id);
        var isFirstApply = applied is null;

        if (isFirstApply)
        {
            // Saved before anything is written, so a crash mid-apply still leaves a way back.
            applied = new AppliedTweak(
                option.Id,
                tweak.Targets.Select(target => new OriginalValue(target.Location, _writes.ReadRegistryValue(target.Location))).ToList());
            _store.Save(tweak.Id, applied);
        }

        try
        {
            WriteAll(_writes, tweak, ValuesOf(tweak, option));
        }
        catch (TweakApplyException error) when (isFirstApply && error.RolledBack)
        {
            // Nothing was applied, so there is nothing to Undo. (When the rollback was incomplete
            // the Original Values are kept, so Undo can still put the machine back.)
            _store.Clear(tweak.Id);
            throw;
        }

        _store.Save(tweak.Id, applied! with { LastAppliedOptionId = option.Id });
    }

    /// <summary>Puts <paramref name="tweak"/>'s targets back to their Original Values, then forgets them. Does nothing if it was not applied.</summary>
    /// <exception cref="TweakApplyException">A write failed; the Original Values are kept so Undo can be tried again.</exception>
    public void Undo(DeclaredTweak tweak)
    {
        if (_store.Get(tweak.Id) is not { } applied)
        {
            return;
        }

        WriteAll(_writes, tweak, applied.OriginalValues.Select(original => (original.Location, original.Value)).ToList());
        _store.Clear(tweak.Id);
    }

    private static List<(RegistryLocation Location, RegistryValue? Value)> ValuesOf(DeclaredTweak tweak, TweakOption option) =>
        tweak.Targets.Select(target => (target.Location, option.Values[target])).ToList();

    /// <summary>
    /// Writes every value (null deletes) as one unit: if one fails, the values already written
    /// are put back to what they held before this call (for a first apply, the Original Values),
    /// so a failed Option switch leaves the Tweak in the Option it was in.
    /// </summary>
    private static void WriteAll(IMachine target, DeclaredTweak tweak, IReadOnlyList<(RegistryLocation Location, RegistryValue? Value)> values)
    {
        var before = values.Select(value => target.ReadRegistryValue(value.Location)).ToList();

        for (var i = 0; i < values.Count; i++)
        {
            try
            {
                Write(target, values[i].Location, values[i].Value);
            }
            catch (Exception ex)
            {
                var notRolledBack = new List<RegistryLocation>();
                for (var written = i - 1; written >= 0; written--)
                {
                    try
                    {
                        Write(target, values[written].Location, before[written]);
                    }
                    catch (Exception)
                    {
                        notRolledBack.Add(values[written].Location);
                    }
                }

                throw new TweakApplyException(tweak, values[i].Location, notRolledBack, ex);
            }
        }
    }

    private static void Write(IMachine target, RegistryLocation location, RegistryValue? value)
    {
        if (value is null)
        {
            target.DeleteRegistryValue(location);
        }
        else
        {
            target.WriteRegistryValue(location, value);
        }
    }

    /// <summary>What Windows behaves as for <paramref name="value"/>, treating a missing value as the target's <see cref="RegistryTarget.AbsentMeans"/>.</summary>
    private static RegistryValue? Effective(RegistryTarget target, RegistryValue? value) =>
        value ?? target.AbsentMeans;
}
