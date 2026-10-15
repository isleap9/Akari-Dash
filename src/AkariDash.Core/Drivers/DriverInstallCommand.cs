using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>
/// The command lines a driver row runs. Kept here beside the rows so the Phase 1 build can show
/// exactly what a real install would have run without running it, as <c>WingetCommand</c> does.
/// </summary>
public static class DriverInstallCommand
{
    /// <summary>
    /// Whether the vendor's installer can run with no one at the keyboard. Only NVIDIA's can:
    /// AkariOS opens AMD's web installer bare (Graphics.ps1:233) and sends Intel to its download
    /// page, so those rows hand off to the vendor's own installer rather than claim a silent one.
    /// </summary>
    public static bool IsUnattended(GpuVendor vendor) => vendor == GpuVendor.Nvidia;

    /// <summary>
    /// The installer's arguments for a row, or none when the vendor cannot install unattended.
    /// NVIDIA's are AkariOS's own (Graphics.ps1:269); the configured row asks for a clean install.
    /// </summary>
    public static IReadOnlyList<string> Install(DriverRow row, string installerPath)
    {
        if (!IsUnattended(row.Vendor))
        {
            return [];
        }

        return row.Kind == DriverStepKind.Install
            ? ["-s", "-noreboot", "-noeula"]
            : ["-s", "-noreboot", "-noeula", "-clean"];
    }

    /// <summary>The line a person would type to run it; what the Phase 1 build reports.</summary>
    public static string Describe(string executable, IReadOnlyList<string> arguments) =>
        string.Join(' ', new[] { executable }.Concat(arguments).Select(Quote));

    private static string Quote(string part) => part.Contains(' ') ? $"\"{part}\"" : part;
}
