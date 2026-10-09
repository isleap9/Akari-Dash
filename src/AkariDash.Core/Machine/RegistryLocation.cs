using Microsoft.Win32;

namespace AkariDash.Core.Machine;

/// <summary>One named registry value: hive, key path and value name.</summary>
public sealed record RegistryLocation(RegistryHive Hive, string Key, string Name)
{
    public override string ToString() => $@"{HiveName}\{Key}\{Name}";

    private string HiveName => Hive switch
    {
        RegistryHive.CurrentUser => "HKCU",
        RegistryHive.LocalMachine => "HKLM",
        _ => Hive.ToString(),
    };
}
