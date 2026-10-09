using AkariDash.Core.Machine;

namespace AkariDash.Tests;

/// <summary>Fake Windows for tests: a registry held in a dictionary.</summary>
public sealed class InMemoryMachine : IMachine
{
    private readonly Dictionary<RegistryLocation, RegistryValue> _registry = [];
    private readonly HashSet<RegistryLocation> _failing = [];

    public InMemoryMachine WithRegistryValue(RegistryLocation location, RegistryValue value)
    {
        _registry[location] = value;
        return this;
    }

    /// <summary>Makes every write or delete of <paramref name="location"/> fail, as access denied would.</summary>
    public InMemoryMachine FailingWritesTo(RegistryLocation location)
    {
        _failing.Add(location);
        return this;
    }

    public RegistryValue? ReadRegistryValue(RegistryLocation location) =>
        _registry.GetValueOrDefault(location);

    public void WriteRegistryValue(RegistryLocation location, RegistryValue value)
    {
        ThrowIfFailing(location);
        _registry[location] = value;
    }

    public void DeleteRegistryValue(RegistryLocation location)
    {
        ThrowIfFailing(location);
        _registry.Remove(location);
    }

    private void ThrowIfFailing(RegistryLocation location)
    {
        if (_failing.Contains(location))
        {
            throw new UnauthorizedAccessException("Access is denied.");
        }
    }
}
