using AkariDash.Core.Machine;
using Microsoft.Win32;

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
    private readonly List<(DeclaredTweak Tweak, Activation Activation)> _changed = [];

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
                    Waiting(Activation.AfterSignOut),
                    Waiting(Activation.AfterRestart));

                List<DeclaredTweak> Waiting(Activation activation) => _changed
                    .Where(change => change.Activation == activation)
                    .Select(change => change.Tweak)
                    .ToList();
            }
        }
    }

    /// <summary>
    /// Reads which Option <paramref name="tweak"/> is in right now, or <see cref="LiveState.Drifted"/>
    /// when it is no longer in the Option Akari-Dash last applied, or <see cref="LiveState.Unavailable"/>
    /// when it cannot be used on this machine, including when Windows does not let it be read.
    /// </summary>
    /// <param name="catalog">
    /// The other Tweaks to read this one against, so that a value one of them has been applied over
    /// is not reported as Drift. Leaving it out reports every move away from the applied Option as
    /// Drift, which is right only when nothing else can have written these values.
    /// </param>
    public LiveState ReadLiveState(DeclaredTweak tweak, IEnumerable<DeclaredTweak>? catalog = null)
    {
        try
        {
            return ReadLiveStateUnguarded(tweak, catalog);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new LiveState.Unavailable(ex.Message);
        }
    }

    private LiveState ReadLiveStateUnguarded(DeclaredTweak tweak, IEnumerable<DeclaredTweak>? catalog)
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
        return stillApplied || ChangedByAnotherTweak(tweak, applied, catalog)
            ? live
            : new LiveState.Drifted(applied, live);
    }

    /// <summary>
    /// Whether every value that has moved away from <paramref name="applied"/> now holds what
    /// another Tweak Akari-Dash has applied put there. Two Tweaks may share a value on purpose — a
    /// preset covers the rows beneath it — and when the user then sets one of those rows,
    /// the other has not drifted: Akari-Dash moved it, on the user's instruction, and offering to
    /// re-apply would undo the choice they just made. Drift means Windows reset it, so this is not
    /// Drift; the Tweak reads as whatever the machine is in now, Undo included.
    /// </summary>
    private bool ChangedByAnotherTweak(DeclaredTweak tweak, TweakOption applied, IEnumerable<DeclaredTweak>? catalog)
    {
        if (catalog is null)
        {
            return false;
        }

        var others = catalog
            .Where(other => other.Id != tweak.Id)
            .Select(other => (Tweak: other, Applied: AppliedOption(other)))
            .Where(other => other.Applied is not null)
            .ToList();

        if (others.Count == 0)
        {
            return false;
        }

        var moved = tweak.Targets
            .Select(target => (Target: target, Value: machine.Read(target.Location)))
            .Where(live => Effective(live.Target, live.Value) != Effective(live.Target, applied.Values[live.Target]))
            .ToList();

        // Nothing moved yet the Tweak is not in its Option: the Options overlap, not our business here.
        return moved.Count > 0 && moved.All(live => others.Any(other =>
            other.Tweak.Targets.FirstOrDefault(target => target.Location == live.Target.Location) is { } theirs &&
            Effective(theirs, other.Applied!.Values[theirs]) == Effective(theirs, live.Value)));
    }

    /// <summary>Lists what applying <paramref name="option"/> would change, writing nothing.</summary>
    /// <exception cref="TweakUnavailableException">The Tweak is Unavailable on this machine.</exception>
    public IReadOnlyList<PlannedChange> Preview(DeclaredTweak tweak, TweakOption option)
    {
        ThrowIfUnavailable(tweak);

        var dryRun = new DryRunMachine(machine);
        WriteAll(dryRun, tweak.Title, ValuesOf(tweak, option));
        return dryRun.PlannedChanges;
    }

    /// <summary>
    /// The machine's own value for <paramref name="location"/>: the Original Value another applied
    /// Tweak already holds for it, or what the machine reads now when none does.
    ///
    /// An Original Value belongs to the location, not to the Tweak, and is the machine's own value —
    /// so two Tweaks that share a location must record the same one. Two Tweaks may share on purpose:
    /// a preset covers the rows beneath it. Reading the machine for the second of them
    /// would record Akari-Dash's own earlier write as if Windows had put it there, and undoing that
    /// Tweak would then restore a state the PC was never in — and, once the first Tweak's record is
    /// gone, leave those values behind with nothing owning them.
    /// </summary>
    private MachineValue? MachinesOwnValue(MachineLocation location, string tweakId) =>
        _store.AppliedTweakIds()
            .Where(id => id != tweakId)
            .Select(_store.Get)
            .OfType<AppliedTweak>()
            .SelectMany(other => other.OriginalValues)
            .FirstOrDefault(original => original.Location == location) is { } recorded
            ? recorded.Value
            : _writes.Read(location);

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

        // An Original Value belongs to a target, not to the Tweak, and is saved just before
        // Akari-Dash first writes that target. So a target the Tweak has gained since it was applied
        // gets its own Original Value read now: without it the apply below would write the target
        // with nothing recorded for it, and Undo would leave that value behind for good.
        List<OriginalValue> originals = [.. applied?.OriginalValues ?? []];
        var newTargets = tweak.Targets
            .Where(target => originals.All(original => original.Location != target.Location))
            .Select(target => new OriginalValue(target.Location, MachinesOwnValue(target.Location, tweak.Id)))
            .ToList();

        if (newTargets.Count > 0)
        {
            originals.AddRange(newTargets);

            // Saved before anything is written, so a crash mid-apply still leaves a way back.
            applied = applied is null
                ? new AppliedTweak(option.Id, originals)
                : applied with { OriginalValues = originals };
            _store.Save(tweak.Id, applied);
        }

        try
        {
            WriteAll(_writes, tweak.Title, ValuesOf(tweak, option));
        }
        catch (TweakApplyException error) when (isFirstApply && error.RolledBack)
        {
            // Nothing was applied, so there is nothing to Undo. (When the rollback was incomplete
            // the Original Values are kept, so Undo can still put the machine back.)
            _store.Clear(tweak.Id);
            throw;
        }

        _store.Save(tweak.Id, applied! with { LastAppliedOptionId = option.Id });
        MarkChanged(tweak, tweak.ActivationOf(option));
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

        List<(MachineLocation Location, MachineValue? Value)> writes =
            [.. applied.OriginalValues.Select(original => (original.Location, original.Value))];
        writes.AddRange(LeftBehind(tweak, applied));

        WriteAll(_writes, tweak.Title, writes);
        _store.Clear(tweak.Id);

        // Putting back what the applied Option wrote waits on whatever that Option waited on.
        var undone = tweak.Options.FirstOrDefault(option => option.Id == applied.LastAppliedOptionId);
        MarkChanged(tweak, undone is null ? tweak.Activation : tweak.ActivationOf(undone));
    }

    /// <summary>
    /// Targets this Tweak writes that its record has no Original Value for, and that still hold
    /// exactly what the record's own Option wrote, so Undo deletes them instead of walking away and
    /// leaving Akari-Dash's value on the machine.
    /// </summary>
    /// <remarks>
    /// A record written by an older Akari-Dash covers only the targets the Tweak had back then, so a
    /// Tweak that has gained one since (as privacy.activity-history gained the user copy of its
    /// policy) can hold a value with nothing recorded for it. Apply records every target now, which
    /// stops it happening again, but a record saved before that cannot be mended after the fact.
    ///
    /// Only a value equal to what the last applied Option writes is touched: that is Akari-Dash's own
    /// write, so "not there" is where it came from. A target holding anything else was set by someone
    /// or something else and is left exactly as it is.
    /// </remarks>
    private IEnumerable<(MachineLocation Location, MachineValue? Value)> LeftBehind(DeclaredTweak tweak, AppliedTweak applied)
    {
        if (tweak.Options.SingleOrDefault(option => option.Id == applied.LastAppliedOptionId) is not { } option)
        {
            yield break;
        }

        foreach (var target in tweak.Targets)
        {
            if (applied.OriginalValues.Any(original => original.Location == target.Location)
                || !option.Values.TryGetValue(target, out var written)
                || written is null)
            {
                continue;
            }

            if (Equals(_writes.Read(target.Location), written))
            {
                yield return (target.Location, null);
            }
        }
    }

    /// <summary>
    /// Tweaks Akari-Dash applied that are no longer in <paramref name="catalog"/>, for example because
    /// a later version dropped them. Their Original Values are still saved, so they can still be undone.
    /// </summary>
    public IReadOnlyList<RetiredTweak> RetiredTweaks(IEnumerable<DeclaredTweak> catalog)
    {
        var known = catalog.Select(tweak => tweak.Id).ToHashSet();

        return _store.AppliedTweakIds()
            .Where(id => !known.Contains(id))
            .Select(id => _store.Get(id) is { } applied ? new RetiredTweak(id, applied.OriginalValues) : null)
            .OfType<RetiredTweak>()
            .ToList();
    }

    /// <summary>
    /// Puts a retired Tweak's targets back to their Original Values, then forgets them, working from
    /// the saved values alone. Does nothing if <paramref name="tweakId"/> has no saved record.
    /// </summary>
    /// <exception cref="TweakApplyException">A write failed; the Original Values are kept so Undo can be tried again.</exception>
    public void UndoRetired(string tweakId)
    {
        lock (_writing)
        {
            if (_store.Get(tweakId) is not { } applied)
            {
                return;
            }

            WriteAll(_writes, tweakId, applied.OriginalValues.Select(original => (original.Location, original.Value)).ToList());
            _store.Clear(tweakId);
        }
    }

    /// <summary>
    /// Creates a restore point now, when the user asks for one (Akari-Dash offers it on first launch).
    /// It counts as the session's restore point, so the first apply does not ask for another.
    /// </summary>
    /// <exception cref="SystemProtectionOffException">System Protection is off; <see cref="TurnOnSystemProtection"/> turns it on.</exception>
    public void CreateRestorePointNow()
    {
        lock (_writing)
        {
            _writes.CreateRestorePoint(RestorePointDescription);
            _restorePointRequested = true;
        }
    }

    /// <summary>Turns System Protection on for the Windows drive, so restore points can be made.</summary>
    public void TurnOnSystemProtection()
    {
        lock (_writing)
        {
            _writes.TurnOnSystemProtection(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\");
        }
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

    /// <summary>
    /// Remembers that a Tweak was changed and what its change is waiting on, which is the applied
    /// Option's Activation rather than the Tweak's where the two differ.
    /// </summary>
    private void MarkChanged(DeclaredTweak tweak, Activation activation)
    {
        lock (_changed)
        {
            var at = _changed.FindIndex(change => change.Tweak == tweak);

            // The latest change decides: picking an Option that waits on a restart, after one that
            // took effect at once, moves the Tweak to the restart list without losing its place.
            if (at >= 0)
            {
                _changed[at] = (tweak, activation);
            }
            else
            {
                _changed.Add((tweak, activation));
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
            .FirstOrDefault(location => location is not (RegistryLocation or RegistryEntryLocation or RegistryKeyLocation or BootSettingLocation or BlackImageLocation or AccountPicturesLocation) && machine.Read(location) is null);

        return missing switch
        {
            ServiceLocation service => $"The {service.ServiceName} service is not installed on this PC.",
            ScheduledTaskLocation task => $"The scheduled task {task.Path} does not exist on this PC.",
            RegistryBitLocation bit => machine.Read(bit.Value) is RegistryValue { Kind: var kind } && kind != RegistryValueKind.Binary
                ? $"{bit.Value} holds {kind} data instead of binary, so its bits cannot be read. Some tweak scripts write it with the wrong type."
                : $"{bit.Value} does not exist on this PC, or is too short.",
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
    private static void WriteAll(IMachine target, string tweakTitle, IReadOnlyList<(MachineLocation Location, MachineValue? Value)> values)
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

                throw new TweakApplyException(tweakTitle, values[i].Location, notRolledBack, ex);
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
        target.Effective(value);
}
