using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>
/// One button on the Drivers page: an AkariOS-Ultimate script, run as written. <paramref name="Vendor"/>
/// limits it to PCs with that maker's card; <paramref name="Confirm"/> is the question asked first.
/// With <paramref name="SafeModeOption"/>, the script only prepares, and a <see cref="DriverClean"/>
/// pass in that mode is queued for the safe-mode companion afterwards.
/// </summary>
public sealed record GraphicsAction(
    string Label,
    string Description,
    string Script,
    GpuVendor? Vendor = null,
    string? Confirm = null,
    string? SafeModeOption = null);

/// <summary>One row of the Drivers page, with its buttons. <paramref name="Vendor"/> limits the whole row.</summary>
public sealed record GraphicsEntry(string Id, string Name, string Description, IReadOnlyList<GraphicsAction> Actions, GpuVendor? Vendor = null);

/// <summary>
/// AkariOS-Ultimate's Graphics category (Tweaks/Graphics.ps1, MIT, (c) FR33THY), entry for entry. Each
/// button runs that entry's own script block, copied 1:1 into the app's Scripts\Graphics folder, with
/// the helpers Akari.ps1 gives it (<see cref="Helpers"/>). These are Actions, not Tweaks (ADR-0001,
/// amended 2026-10-19): Revert is the script's own, and nothing is read back. DDU's safe-mode half
/// is the one part not run from its script: see <see cref="DriverClean"/>.
/// Left out on purpose: C++ redistributables (on the Downloads page), and the two links that open
/// Settings (HAGS is a Gaming Tweak; resolution belongs to the GPUs page).
/// </summary>
public static class GraphicsEntries
{
    /// <summary>The helpers every script runs after: Write-Log, Pause, Read-Host, Get-FilePath...</summary>
    public const string Helpers = "_helpers.ps1";

    private static GraphicsAction Apply(string id) => new("Apply", "Apply", $"{id}-apply.ps1");

    private static GraphicsAction Revert(string id) => new("Revert", "Back to the driver's default", $"{id}-revert.ps1");

    public static IReadOnlyList<GraphicsEntry> All { get; } =
    [
        new("driver-clean", "Driver clean (DDU)", "Remove the current graphics driver with DDU",
        [
            new("DDU: Auto", "Run DDU automatically", "driver-clean-prepare.ps1",
                Confirm: "DDU will remove your graphics driver and restart the PC. Continue?", SafeModeOption: DriverClean.Auto),
            new("DDU: Manual", "Open DDU to clean manually", "driver-clean-prepare.ps1",
                Confirm: "DDU will remove your graphics driver. Continue?", SafeModeOption: DriverClean.Manual),
        ]),
        new("driver-install", "Driver install (updated)", "Install the latest graphics driver",
        [
            new("NVIDIA", "Driver install (updated): NVIDIA", "driver-install-nvidia.ps1", GpuVendor.Nvidia),
            new("AMD", "Driver install (updated): AMD", "driver-install-amd.ps1", GpuVendor.Amd),
            new("INTEL", "Driver install (updated): INTEL", "driver-install-intel.ps1", GpuVendor.Intel),
        ]),
        new("driver-install-settings", "Driver install and settings (updated)", "Install the latest graphics driver and import optimized settings",
        [
            new("NVIDIA", "Download, install, and configure the latest NVIDIA driver", "driver-install-settings-nvidia.ps1", GpuVendor.Nvidia),
            new("AMD", "Download, install, and configure the latest AMD driver", "driver-install-settings-amd.ps1", GpuVendor.Amd),
            new("Intel", "Open Intel driver download page, select downloaded driver, install and configure", "driver-install-settings-intel.ps1", GpuVendor.Intel),
        ]),
        new("driver-debloat", "Driver debloat install and settings", "Install a debloated graphics driver and import optimized settings",
        [
            new("NVIDIA", "Select a downloaded NVIDIA driver, debloat it, install and configure", "driver-debloat-nvidia.ps1", GpuVendor.Nvidia),
            new("AMD", "Select a downloaded AMD driver, debloat it, install and configure", "driver-debloat-amd.ps1", GpuVendor.Amd),
            new("Intel", "Select a downloaded Intel driver, debloat it, install and configure", "driver-debloat-intel.ps1", GpuVendor.Intel),
        ]),
        new("nvidia-settings", "NVIDIA settings", "Apply optimized NVIDIA driver settings",
            [Apply("nvidia-settings"), Revert("nvidia-settings")], GpuVendor.Nvidia),
        new("amd-settings", "AMD settings", "Apply optimized AMD driver settings",
            [Apply("amd-settings"), Revert("amd-settings")], GpuVendor.Amd),
        new("intel-settings", "Intel settings", "Apply optimized Intel graphics settings",
            [Apply("intel-settings"), Revert("intel-settings")], GpuVendor.Intel),
        new("hdcp", "HDCP", "Turn NVIDIA HDCP off", [Apply("hdcp"), Revert("hdcp")], GpuVendor.Nvidia),
        new("p0-state", "P0 state", "Keep the NVIDIA GPU at its highest performance power state",
            [Apply("p0-state"), Revert("p0-state")], GpuVendor.Nvidia),
        new("msi-mode", "MSI mode", "Turn MSI mode on for the GPU (Revert turns it off)", [Apply("msi-mode"), Revert("msi-mode")]),
        new("directx", "DirectX", "Download and install DirectX", [new("Install", "Download and install DirectX", "directx-apply.ps1")]),
    ];

    /// <summary>
    /// The entries a PC with cards from <paramref name="vendors"/> is shown: another maker's rows and
    /// buttons are left out, and a row with no button left goes too.
    /// </summary>
    public static IReadOnlyList<GraphicsEntry> For(IReadOnlySet<GpuVendor> vendors)
    {
        bool Fits(GpuVendor? vendor) => vendor is null || vendors.Contains(vendor.Value);

        return All
            .Where(entry => Fits(entry.Vendor))
            .Select(entry => entry with { Actions = entry.Actions.Where(action => Fits(action.Vendor)).ToList() })
            .Where(entry => entry.Actions.Count > 0)
            .ToList();
    }
}
