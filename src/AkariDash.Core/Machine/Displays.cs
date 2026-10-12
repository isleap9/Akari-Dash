using Microsoft.Win32;

namespace AkariDash.Core.Machine;

/// <summary>The displays Windows keeps per-display settings for, such as automatic color management.</summary>
public static class Displays
{
    /// <summary>Where Windows keeps one subkey per display it has seen, named by its hardware id.</summary>
    public const string MonitorDataStore = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore";

    /// <summary>The hardware ids of every display Windows keeps settings for (empty when none can be read).</summary>
    public static IReadOnlyList<string> Known()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var store = baseKey.OpenSubKey(MonitorDataStore);
            return store?.GetSubKeyNames().Order(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }
}
