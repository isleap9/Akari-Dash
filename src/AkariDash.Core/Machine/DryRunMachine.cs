namespace AkariDash.Core.Machine;

/// <summary>
/// Reads the wrapped machine but writes nothing to it: every write is recorded as a
/// <see cref="PlannedChange"/> instead, and later reads see those recorded writes.
/// </summary>
public sealed class DryRunMachine(IMachine inner) : IMachine
{
    // One entry per location, in the order first written: Before is captured at the first
    // write, After is the latest recorded value (null is a recorded delete).
    private readonly List<PlannedChange> _changes = [];

    /// <summary>Every change recorded so far, one per registry value, in the order first changed.</summary>
    public IReadOnlyList<PlannedChange> PlannedChanges =>
        _changes.Where(change => change.Before != change.After).ToList();

    public RegistryValue? ReadRegistryValue(RegistryLocation location) =>
        Find(location) is { } change ? change.After : inner.ReadRegistryValue(location);

    public void WriteRegistryValue(RegistryLocation location, RegistryValue value) => Record(location, value);

    public void DeleteRegistryValue(RegistryLocation location) => Record(location, null);

    private void Record(RegistryLocation location, RegistryValue? value)
    {
        var index = _changes.FindIndex(change => change.Location == location);
        if (index < 0)
        {
            _changes.Add(new PlannedChange(location, inner.ReadRegistryValue(location), value));
        }
        else
        {
            _changes[index] = _changes[index] with { After = value };
        }
    }

    private PlannedChange? Find(RegistryLocation location) =>
        _changes.FirstOrDefault(change => change.Location == location);
}
