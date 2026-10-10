using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// Runs Tweaks against a <see cref="IMachine"/>. Live State and previews always read
/// <paramref name="machine"/>; <see cref="Apply"/> and <see cref="Undo"/> write to
/// <paramref name="applyTo"/> (a <see cref="DryRunMachine"/> in Phase 1), or to
/// <paramref name="machine"/> when not given. Original Values are kept in
/// <paramref name="store"/> (this session only when not given). One engine is one session: the
/// first apply that writes to the machine asks it for a restore point first.
/// </summary>
public sealed class TweakEngine(IMachine machine, IOriginalValuesStore? store = null, IMachine? applyTo = null)
{
    private readonly IOriginalValuesStore _store = store ?? new InMemoryOriginalValuesStore();
    private readonly IMachine _writes = applyTo ?? machine;
    private readonly List<DeclaredTweak> _changed = [];

    // Applies and Undos may run off the UI thread (the first waits for a restore point), so they
    // take turns: no write starts before the session's restore point has been asked for.
    private readonly Lock _writing = new();
    private volatile bool _restorePointRequested;
    private Exception? _restorePointFailure;

    /// <summary>The restore point's description, as listed in Windows' System Restore.</summary>
    public const string RestorePointDescription = "Akari-Dash: before applying Tweaks";

    /// <summary>
    /// The Tweaks applied or undone through this engine (so, this session) whose change waits on a
    /// sign-out or restart. Immediate Tweaks never appear here.
    /// </summary>
    public PendingActivation Pending
    {
        get
        {
            lock (_changed)
            {
                return new(
                    _changed.Where(tweak => tweak.Activation == Activation.AfterSignOut).ToList(),
                    _changed.Where(tweak => tweak.Activation == Activation.AfterRestart).ToList());
            }
        }
    }

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
        lock (_writing)
        {
            ApplyInTurn(tweak, option);
        }
    }

    private void ApplyInTurn(DeclaredTweak tweak, TweakOption option)
    {
        ThrowIfUnavailable(tweak);
        RequestRestorePointOnce();

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
        MarkChanged(tweak);
    }

    /// <summary>Puts <paramref name="tweak"/>'s targets back to their Original Values, then forgets them. Does nothing if it was not applied.</summary>
    /// <exception cref="TweakApplyException">A write failed; the Original Values are kept so Undo can be tried again.</exception>
    public void Undo(DeclaredTweak tweak)
    {
        lock (_writing)
        {
            UndoInTurn(tweak);
        }
    }

    private void UndoInTurn(DeclaredTweak tweak)
    {
        if (_store.Get(tweak.Id) is not { } applied)
        {
            return;
        }

        WriteAll(_writes, tweak, applied.OriginalValues.Select(original => (original.Location, original.Value)).ToList());
        _store.Clear(tweak.Id);
        MarkChanged(tweak);
    }

    /// <summary>Whether the next apply will first wait for a restore point (never for a Dry Run).</summary>
    public bool RestorePointDue => !_restorePointRequested && _writes is not DryRunMachine;

    /// <summary>
    /// Why the session's restore point could not be created, the first time it is asked after the
    /// failure; <see langword="null"/> otherwise (created, not yet requested, or already reported).
    /// </summary>
    public Exception? TakeRestorePointFailure()
    {
        return Interlocked.Exchange(ref _restorePointFailure, null);
    }

    /// <summary>
    /// Asks the machine applies write to for a restore point, once per session (a Dry Run ignores
    /// it). A failure is kept for <see cref="TakeRestorePointFailure"/> and never stops the apply,
    /// nor is it retried.
    /// </summary>
    private void RequestRestorePointOnce()
    {
        if (_restorePointRequested)
        {
            return;
        }

        _restorePointRequested = true;
        try
        {
            _writes.CreateRestorePoint(RestorePointDescription);
        }
        catch (Exception ex)
        {
            _restorePointFailure = ex;
        }
    }

    private void MarkChanged(DeclaredTweak tweak)
    {
        lock (_changed)
        {
            if (!_changed.Contains(tweak))
            {
                _changed.Add(tweak);
            }
        }
    }

    /// <summary>
    /// The combined preview for putting each of <paramref name="tweaks"/> into its Recommended Option,
    /// writing nothing. Unavailable Tweaks, Tweaks without a Recommended Option, Tweaks already in
    /// it, and Tweaks whose values cannot be read are skipped.
    /// </summary>
    public RecommendedPlan PlanRecommended(IEnumerable<DeclaredTweak> tweaks)
    {
        var toApply = new List<PlannedTweak>();
        var skipped = new List<SkippedTweak>();

        foreach (var tweak in tweaks)
        {
            try
            {
                Plan(tweak);
            }
            catch (Exception ex)
            {
                skipped.Add(new SkippedTweak(tweak, $"Could not read its current values: {ex.Message}"));
            }
        }

        return new RecommendedPlan(toApply, skipped);

        void Plan(DeclaredTweak tweak)
        {
            if (UnavailableReason(tweak) is { } reason)
            {
                skipped.Add(new SkippedTweak(tweak, reason));
            }
            else if (tweak.Recommended is not { } recommended)
            {
                skipped.Add(new SkippedTweak(tweak, "No Recommended Option; this one is a matter of taste."));
            }
            else if (Preview(tweak, recommended) is { Count: > 0 } changes)
            {
                toApply.Add(new PlannedTweak(tweak, recommended, changes));
            }
            else
            {
                skipped.Add(new SkippedTweak(tweak, $"Already {recommended.Label}."));
            }
        }
    }

    /// <summary>
    /// Applies every Tweak in <paramref name="plan"/>, one at a time, and reports how each went.
    /// A failing Tweak is rolled back on its own (see <see cref="Apply"/>) and does not stop the rest.
    /// </summary>
    public IReadOnlyList<TweakResult> ApplyRecommended(RecommendedPlan plan) =>
        plan.ToApply.Select(planned =>
        {
            try
            {
                Apply(planned.Tweak, planned.Option);
                return new TweakResult(planned.Tweak, planned.Option, null);
            }
            catch (Exception ex)
            {
                return new TweakResult(planned.Tweak, planned.Option, ex);
            }
        }).ToList();

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
