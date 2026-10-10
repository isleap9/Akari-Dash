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

    /// <summary>
    /// Reads which Option <paramref name="tweak"/> is in right now, or <see cref="LiveState.Drifted"/>
    /// when it is no longer in the Option Akari-Dash last applied, or <see cref="LiveState.Unavailable"/>
    /// when it cannot be used on this machine.
    /// </summary>
    public LiveState ReadLiveState(DeclaredTweak tweak)
    {
        if (UnavailableReason(tweak) is { } reason)
        {
            return new LiveState.Unavailable(reason);
        }

        var live = ReadFrom(machine, tweak);

        // Applies that went to a Dry Run never reached the machine, so there is nothing to drift.
        if (!ReferenceEquals(_writes, machine) || AppliedOption(tweak) is not { } applied)
        {
            return live;
        }

        var stillApplied = live is LiveState.InOption current && current.Option == applied;
        return stillApplied ? live : new LiveState.Drifted(applied, live);
    }

    /// <summary>Lists what applying <paramref name="option"/> would change, writing nothing.</summary>
    /// <exception cref="TweakUnavailableException">The Tweak is Unavailable on this machine.</exception>
    public IReadOnlyList<PlannedChange> Preview(DeclaredTweak tweak, TweakOption option)
    {
        ThrowIfUnavailable(tweak);

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
    /// <exception cref="TweakUnavailableException">The Tweak is Unavailable on this machine; nothing was written or saved.</exception>
    /// <exception cref="TweakApplyException">A write failed; every target already written was rolled back.</exception>
    public void Apply(DeclaredTweak tweak, TweakOption option)
    {
        ThrowIfUnavailable(tweak);

        var applied = _store.Get(tweak.Id);
        var isFirstApply = applied is null;

        if (isFirstApply)
        {
            // Saved before anything is written, so a crash mid-apply still leaves a way back.
            applied = new AppliedTweak(
                option.Id,
                tweak.Targets.Select(target => new OriginalValue(target.Location, _writes.Read(target.Location))).ToList());
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

    /// <summary>
    /// Why <paramref name="tweak"/> cannot be used on this machine, or <see langword="null"/> when it can.
    /// A missing registry value is normal (it counts as <see cref="TweakTarget.AbsentMeans"/>), but a
    /// service or scheduled task cannot be created, so one that does not exist leaves nothing to change.
    /// </summary>
    private string? UnavailableReason(DeclaredTweak tweak)
    {
        if (tweak.RequiresGpu is { } required)
        {
            var vendors = machine.GpuVendors();
            if (!vendors.Contains(required))
            {
                var found = vendors.Count == 0
                    ? "none was found on this PC"
                    : $"this PC has {string.Join(" and ", vendors.Order().Select(vendor => vendor.DisplayName()))}";
                return $"Needs an {required.DisplayName()} graphics card; {found}.";
            }
        }

        var missing = tweak.Targets
            .Select(target => target.Location)
            .FirstOrDefault(location => location is not RegistryLocation && machine.Read(location) is null);

        return missing switch
        {
            ServiceLocation service => $"The {service.ServiceName} service is not installed on this PC.",
            ScheduledTaskLocation task => $"The scheduled task {task.Path} does not exist on this PC.",
            null => null,
            _ => $"{missing} does not exist on this PC.",
        };
    }

    private void ThrowIfUnavailable(DeclaredTweak tweak)
    {
        if (UnavailableReason(tweak) is { } reason)
        {
            throw new TweakUnavailableException(tweak, reason);
        }
    }

    private static LiveState ReadFrom(IMachine source, DeclaredTweak tweak)
    {
        var live = tweak.Targets
            .Select(target => new TargetValue(target, source.Read(target.Location)))
            .ToList();

        var option = tweak.Options.FirstOrDefault(option =>
            live.All(value => Effective(value.Target, value.Value) == Effective(value.Target, option.Values[value.Target])));

        return option is null ? new LiveState.Custom(live) : new LiveState.InOption(option);
    }

    private static List<(MachineLocation Location, MachineValue? Value)> ValuesOf(DeclaredTweak tweak, TweakOption option) =>
        tweak.Targets.Select(target => (target.Location, option.Values[target])).ToList();

    /// <summary>
    /// Writes every value (null deletes) as one unit: if one fails, the values already written
    /// are put back to what they held before this call (for a first apply, the Original Values),
    /// so a failed Option switch leaves the Tweak in the Option it was in.
    /// </summary>
    private static void WriteAll(IMachine target, DeclaredTweak tweak, IReadOnlyList<(MachineLocation Location, MachineValue? Value)> values)
    {
        var before = values.Select(value => target.Read(value.Location)).ToList();

        for (var i = 0; i < values.Count; i++)
        {
            try
            {
                Write(target, values[i].Location, values[i].Value);
            }
            catch (Exception ex)
            {
                var notRolledBack = new List<MachineLocation>();
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

    private static void Write(IMachine target, MachineLocation location, MachineValue? value)
    {
        if (value is null)
        {
            target.Delete(location);
        }
        else
        {
            target.Write(location, value);
        }
    }

    /// <summary>What Windows behaves as for <paramref name="value"/>, treating a missing value as the target's <see cref="TweakTarget.AbsentMeans"/>.</summary>
    private static MachineValue? Effective(TweakTarget target, MachineValue? value) =>
        value ?? target.AbsentMeans;
}
