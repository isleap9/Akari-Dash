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
}
