namespace AkariDash.Core.Apps;

/// <summary>
/// A third-party driver package in the Windows driver store: its published name (oem12.inf), the INF
/// it was installed from, the device it serves when one is attached, and its class, provider, version
/// and date.
/// </summary>
public sealed record DriverPackage(
    string PublishedName,
    string OriginalName,
    string? DeviceName,
    string ClassName,
    string Provider,
    string Version,
    DateTime? Date)
{
    /// <summary>What to call it: the device it serves, or its published name when nothing is attached.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(DeviceName) ? PublishedName : DeviceName!;

    /// <summary>
    /// Classes whose driver a PC cannot do without while it runs: the graphics card, the network, the
    /// storage the system boots from. Deleting one of these says so twice before it goes ahead.
    /// </summary>
    public bool IsCritical => ClassName is "Display" or "Net" or "SCSIAdapter" or "HDC" or "System" or "DiskDrive" or "USB";
}

/// <summary>A device Windows remembers but that is not plugged in now, a "ghost" in Device Manager's hidden view.</summary>
public sealed record GhostDevice(string InstanceId, string Name, string ClassName);

/// <summary>
/// The Drivers and Devices tabs' backend, after Vain Toolbox's: driver packages to export or delete,
/// and disconnected devices to clean up. An Action surface (ADR-0004): nothing here is undone.
/// </summary>
public interface ISystemCleanupService
{
    Task<IReadOnlyList<DriverPackage>> ListDriversAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes the package from the driver store, uninstalling it from the devices that use it.</summary>
    Task DeleteDriverAsync(DriverPackage driver, CancellationToken cancellationToken = default);

    /// <summary>Copies the package to a folder of its own under <paramref name="folder"/>, ready to install again.</summary>
    Task ExportDriverAsync(DriverPackage driver, string folder, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GhostDevice>> ListGhostDevicesAsync(CancellationToken cancellationToken = default);

    Task RemoveDeviceAsync(GhostDevice device, CancellationToken cancellationToken = default);
}
