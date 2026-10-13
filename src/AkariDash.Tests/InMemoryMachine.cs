using AkariDash.Core.Machine;

namespace AkariDash.Tests;

/// <summary>
/// Fake Windows for tests: registry values, service start types and scheduled tasks held in a
/// dictionary. A bit of a binary value is read from and written into the whole value, as on Windows.
/// </summary>
public sealed class InMemoryMachine : IMachine
{
    private readonly Dictionary<MachineLocation, MachineValue> _values = [];
    private readonly HashSet<MachineLocation> _failing = [];
    private readonly HashSet<MachineLocation> _unreadable = [];
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
    /// <summary>Makes every read of <paramref name="location"/> fail, as a key only SYSTEM may read would.</summary>
    public InMemoryMachine FailingReadsOf(MachineLocation location)
    {
        _unreadable.Add(location);
        return this;
    }

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

    public MachineValue? Read(MachineLocation location) => _unreadable.Contains(location)
        ? throw new UnauthorizedAccessException($"Windows does not let Akari-Dash read {location}.")
        : location switch
        {
            RegistryBitLocation bit => bit.ReadFrom(_values.GetValueOrDefault(bit.Value)),
            RegistryEntryLocation entry => entry.ReadFrom(_values.GetValueOrDefault(entry.Value)),
            // Like the registry, a key exists when it was created or when anything is stored under it.
            RegistryKeyLocation key => _values.ContainsKey(key) || _values.Keys.Any(other => IsUnder(other, key)) ? KeyPresence.Present : null,
            _ => _values.GetValueOrDefault(location),
        };

    // Like the real machine, services and scheduled tasks can only be changed, never created or deleted.
    public void Write(MachineLocation location, MachineValue value)
    {
        ThrowIfFailing(location);
        if (location is RegistryEntryLocation entry)
        {
            SetEntry(entry, ((EntryValue)value).Text);
            return;
        }

        if (location is RegistryBitLocation bit)
        {
            _values[bit.Value] = bit.WriteInto(_values.GetValueOrDefault(bit.Value), ((BitValue)value).IsSet);
            return;
        }

        if (location is not (RegistryLocation or RegistryKeyLocation or BootSettingLocation) && !_values.ContainsKey(location))
        {
            throw new InvalidOperationException($"{location} does not exist.");
        }

        _values[location] = value;
    }

    public void Delete(MachineLocation location)
    {
        ThrowIfFailing(location);
        if (location is RegistryEntryLocation entry)
        {
            SetEntry(entry, null);
            return;
        }

        // Like the registry, deleting a key takes everything under it with it.
        if (location is RegistryKeyLocation key)
        {
            foreach (var under in _values.Keys.Where(other => other == key || IsUnder(other, key)).ToList())
            {
                _values.Remove(under);
            }

            return;
        }

        if (location is not (RegistryLocation or BootSettingLocation) && Read(location) is not null)
        {
            throw new NotSupportedException($"{location} cannot be deleted.");
        }

        _values.Remove(location);
    }

    private static bool IsUnder(MachineLocation location, RegistryKeyLocation key)
    {
        var (hive, path) = location switch
        {
            RegistryLocation value => (value.Hive, value.Key),
            RegistryKeyLocation other => (other.Hive, other.Key),
            _ => ((Microsoft.Win32.RegistryHive?)null, string.Empty),
        };

        return hive == key.Hive &&
            (path.Equals(key.Key, StringComparison.OrdinalIgnoreCase) || path.StartsWith(key.Key + @"\", StringComparison.OrdinalIgnoreCase));
    }

    private void SetEntry(RegistryEntryLocation entry, string? text)
    {
        if (entry.WriteInto(_values.GetValueOrDefault(entry.Value), text) is { } whole)
        {
            _values[entry.Value] = whole;
        }
        else
        {
            _values.Remove(entry.Value);
        }
    }

    private void ThrowIfFailing(MachineLocation location)
    {
        if (_failing.Contains(location))
        {
            throw new UnauthorizedAccessException("Access is denied.");
        }
    }
}
