using AkariDash.Core.Machine;

namespace AkariDash.Tests;

/// <summary>Fake Windows for tests: registry values, service start types and scheduled tasks held in a dictionary.</summary>
public sealed class InMemoryMachine : IMachine
{
    private readonly Dictionary<MachineLocation, MachineValue> _values = [];
    private readonly HashSet<MachineLocation> _failing = [];
    private readonly HashSet<GpuVendor> _gpuVendors = [];
    private readonly List<string> _restorePoints = [];
    private bool _restorePointsFail;
    private PcFacts _pc = PcFacts.Unknown;

    /// <summary>The description of every restore point asked for, in order (including ones that failed).</summary>
    public IReadOnlyList<string> RestorePointsRequested => _restorePoints;

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

    /// <summary>Adds a graphics card made by <paramref name="vendor"/>; a new machine has none.</summary>
    public InMemoryMachine WithGpu(GpuVendor vendor)
    {
        _gpuVendors.Add(vendor);
        return this;
    }

    /// <summary>Makes the machine describe itself as <paramref name="pc"/>; a new machine reports nothing.</summary>
    public InMemoryMachine WithPc(PcFacts pc)
    {
        _pc = pc;
        return this;
    }

    /// <summary>Makes every restore point fail, as it would with System Restore turned off.</summary>
    public InMemoryMachine FailingRestorePoints()
    {
        _restorePointsFail = true;
        return this;
    }

    public IReadOnlySet<GpuVendor> GpuVendors() => _gpuVendors;

    public PcFacts DescribePc() => _pc;

    public void CreateRestorePoint(string description)
    {
        _restorePoints.Add(description);
        if (_restorePointsFail)
        {
            throw new InvalidOperationException("System Restore is turned off.");
        }
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
