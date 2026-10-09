using AkariDash.Core.Machine;

namespace AkariDash.Tests;

/// <summary>Fake Windows for tests: a registry held in a dictionary.</summary>
public sealed class InMemoryMachine : IMachine
{
    private readonly Dictionary<RegistryLocation, RegistryValue> _registry = [];

    public InMemoryMachine WithRegistryValue(RegistryLocation location, RegistryValue value)
    {
        _registry[location] = value;
        return this;
    }

    public RegistryValue? ReadRegistryValue(RegistryLocation location) =>
        _registry.GetValueOrDefault(location);
}
