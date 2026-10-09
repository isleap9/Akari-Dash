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

    public void WriteRegistryValue(RegistryLocation location, RegistryValue value)
    {
        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey(location.Key, writable: true);
        key.SetValue(location.Name, value.Data, value.Kind);
    }

    public void DeleteRegistryValue(RegistryLocation location)
    {
        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(location.Key, writable: true);
        key?.DeleteValue(location.Name, throwOnMissingValue: false);
    }
}
