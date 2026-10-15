namespace AkariDash.Core.Machine;

/// <summary>Who made a graphics card, identified by its PCI vendor ID.</summary>
public enum GpuVendor
{
    Nvidia,
    Amd,
    Intel,
}

public static class GpuVendorExtensions
{
    /// <summary>The vendor's own spelling, e.g. <c>NVIDIA</c>.</summary>
    public static string DisplayName(this GpuVendor vendor) => vendor switch
    {
        GpuVendor.Nvidia => "NVIDIA",
        GpuVendor.Amd => "AMD",
        _ => vendor.ToString(),
    };

    /// <summary>
    /// The vendor a display adapter's <c>MatchingDeviceId</c> names (e.g. <c>pci\ven_10de&amp;dev_2484</c>),
    /// or <see langword="null"/> for an adapter that is not a PCI card, such as the Basic Display Adapter.
    /// </summary>
    public static GpuVendor? FromMatchingDeviceId(string? matchingDeviceId)
    {
        var deviceId = matchingDeviceId?.ToUpperInvariant() ?? string.Empty;
        return deviceId switch
        {
            _ when deviceId.Contains("VEN_10DE") => GpuVendor.Nvidia,
            _ when deviceId.Contains("VEN_1002") => GpuVendor.Amd,
            _ when deviceId.Contains("VEN_8086") => GpuVendor.Intel,
            _ => null,
        };
    }
}
