using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>
/// The rows the Drivers page offers: for each vendor, clean the old driver away with DDU (on its
/// own, or with DDU opened for the person to drive), install the latest driver, install and
/// configure it, and debloat a driver the user already downloaded. Curated (ADR-0005): the page
/// shows only the rows for the card that is actually in the PC, so a laptop with an integrated and
/// a discrete card gets one vendor's rows each, never the other vendor's.
/// </summary>
public static class DriverRows
{
    public static IReadOnlyList<DriverRow> All { get; } =
    [
        new("nvidia.clean", GpuVendor.Nvidia, DriverStepKind.Clean, "Clean the NVIDIA driver with DDU", "Restarts into safe mode, where DDU removes the NVIDIA driver completely, then restarts again.", DriverRisk.Advanced),
        new("nvidia.clean-manual", GpuVendor.Nvidia, DriverStepKind.CleanManual, "Open DDU in safe mode", "Restarts into safe mode and opens DDU for you to clean the NVIDIA driver yourself.", DriverRisk.Advanced),
        new("nvidia.install", GpuVendor.Nvidia, DriverStepKind.Install, "Install the latest NVIDIA driver", "Finds the newest NVIDIA driver for this card and installs it quietly.", DriverRisk.Caution),
        new("nvidia.install-and-configure", GpuVendor.Nvidia, DriverStepKind.InstallAndConfigure, "Install and configure", "Installs the newest driver and puts the NVIDIA settings profile on with it.", DriverRisk.Caution),
        new("nvidia.debloat", GpuVendor.Nvidia, DriverStepKind.Debloat, "Install a driver you downloaded", "Strips the extras from an NVIDIA installer you downloaded, then installs what is left.", DriverRisk.Advanced),

        new("amd.clean", GpuVendor.Amd, DriverStepKind.Clean, "Clean the AMD driver with DDU", "Restarts into safe mode, where DDU removes the AMD driver completely, then restarts again.", DriverRisk.Advanced),
        new("amd.clean-manual", GpuVendor.Amd, DriverStepKind.CleanManual, "Open DDU in safe mode", "Restarts into safe mode and opens DDU for you to clean the AMD driver yourself.", DriverRisk.Advanced),
        new("amd.install", GpuVendor.Amd, DriverStepKind.Install, "Install the latest AMD driver", "Finds the newest AMD driver for this card and installs it quietly.", DriverRisk.Caution),
        new("amd.install-and-configure", GpuVendor.Amd, DriverStepKind.InstallAndConfigure, "Install and configure", "Installs the newest driver and puts the AMD settings profile on with it.", DriverRisk.Caution),
        new("amd.debloat", GpuVendor.Amd, DriverStepKind.Debloat, "Install a driver you downloaded", "Strips the extras from an AMD installer you downloaded, then installs what is left.", DriverRisk.Advanced),

        new("intel.clean", GpuVendor.Intel, DriverStepKind.Clean, "Clean the Intel driver with DDU", "Restarts into safe mode, where DDU removes the Intel driver completely, then restarts again.", DriverRisk.Advanced),
        new("intel.clean-manual", GpuVendor.Intel, DriverStepKind.CleanManual, "Open DDU in safe mode", "Restarts into safe mode and opens DDU for you to clean the Intel driver yourself.", DriverRisk.Advanced),
        new("intel.install", GpuVendor.Intel, DriverStepKind.Install, "Install the latest Intel driver", "Finds the newest Intel driver for this card and installs it quietly.", DriverRisk.Caution),
        new("intel.install-and-configure", GpuVendor.Intel, DriverStepKind.InstallAndConfigure, "Install and configure", "Installs the newest driver and puts the Intel settings profile on with it.", DriverRisk.Caution),
        new("intel.debloat", GpuVendor.Intel, DriverStepKind.Debloat, "Install a driver you downloaded", "Strips the extras from an Intel installer you downloaded, then installs what is left.", DriverRisk.Advanced),
    ];

    public static IReadOnlyList<DriverRow> For(GpuVendor vendor) =>
        All.Where(row => row.Vendor == vendor).ToList();
}
