using AkariDash.Core.Machine;

namespace AkariDash.Tests;

/// <summary>Fake Windows for tests: registry values, service start types and scheduled tasks held in a dictionary.</summary>
public sealed class InMemoryMachine : IMachine
{
    private readonly Dictionary<MachineLocation, MachineValue> _values = [];
    private readonly HashSet<MachineLocation> _failing = [];

    public InMemoryMachine With(MachineLocation location, MachineValue value)
    {
        _values[location] = value;
        return this;
    }

    /// <summary>Makes every write or delete of <paramref name="location"/> fail, as access denied would.</summary>
    public InMemoryMachine FailingWritesTo(MachineLocation location)
    {
        _failing.Add(location);
        return this;
    }

    public MachineValue? Read(MachineLocation location) =>
        _values.GetValueOrDefault(location);

    // Like the real machine, services and scheduled tasks can only be changed, never created or deleted.
    public void Write(MachineLocation location, MachineValue value)
    {
        ThrowIfFailing(location);
        if (location is not RegistryLocation && !_values.ContainsKey(location))
        {
            throw new InvalidOperationException($"{location} does not exist.");
        }

        _values[location] = value;
    }

    public void Delete(MachineLocation location)
    {
        ThrowIfFailing(location);
        if (location is not RegistryLocation && _values.ContainsKey(location))
        {
            throw new NotSupportedException($"{location} cannot be deleted.");
        }

        _values.Remove(location);
    }

    private void ThrowIfFailing(MachineLocation location)
    {
        if (_failing.Contains(location))
        {
            throw new UnauthorizedAccessException("Access is denied.");
        }
    }
}
