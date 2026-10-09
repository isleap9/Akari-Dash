using Microsoft.Win32;

namespace AkariDash.Core.Machine;

/// <summary>The real machine: the Windows registry. Verified manually in a VM only.</summary>
public sealed class WindowsMachine : IMachine
{
    public RegistryValue? ReadRegistryValue(RegistryLocation location)
    {
        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(location.Key, writable: false);

        var data = key?.GetValue(location.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return data is null ? null : new RegistryValue(key!.GetValueKind(location.Name), data);
    }

    // Real writes arrive with real apply, Original Values and Undo (#5); until then every
    // build applies through a DryRunMachine.
    public void WriteRegistryValue(RegistryLocation location, RegistryValue value) =>
        throw new NotSupportedException("Real writes are not implemented yet.");

    public void DeleteRegistryValue(RegistryLocation location) =>
        throw new NotSupportedException("Real writes are not implemented yet.");
}
