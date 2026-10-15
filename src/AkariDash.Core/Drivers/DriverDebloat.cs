using System.Text.RegularExpressions;
using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>
/// What a debloat does to a vendor installer once it is extracted: the folders and files it
/// removes, the components it switches off in the installer's own config, and the setup it then
/// runs, then (Intel only) the control app shipped beside it. Every path is relative to the
/// extraction folder; the service decides where that is.
/// </summary>
public sealed record DebloatPlan(
    IReadOnlyList<string> RemoveAfterExtracting,
    IReadOnlyList<string> TurnOffInConfig,
    string Setup,
    IReadOnlyList<string> SetupArguments,
    string? AfterSetup = null,
    IReadOnlyList<string>? AfterSetupArguments = null);

/// <summary>
/// The debloat strip lists, transcribed from AkariOS's driver-debloat entry (Graphics.ps1:483-640).
/// A file is recognised by its vendor's own naming; anything else, another vendor's installer
/// included, gets no plan, so one vendor's list is never applied to another's file.
/// </summary>
public static partial class DriverDebloat
{
    // NVIDIA's extracted package (Graphics.ps1:508-536), all of it: NvCpl goes too, because the
    // NVIDIA Control Panel is installed afterwards from its own package (DriverExtras).
    private static readonly DebloatPlan Nvidia = new(
        RemoveAfterExtracting:
        [
            "Display.Nview",
            "FrameViewSDK",
            "HDAudio",
            "MSVCRT",
            "NvApp.MessageBus",
            "NvBackend",
            "NvContainer",
            "NvCpl",
            "NvDLISR",
            "NVPCF",
            "NvTelemetry",
            "NvVAD",
            "PhysX",
            "PPC",
            "ShadowPlay",
            @"NvApp\CEF",
            @"NvApp\osc",
            @"NvApp\Plugins",
            @"NvApp\UpgradeConsent",
            @"NvApp\www",
            @"NvApp\7z.dll",
            @"NvApp\7z.exe",
            @"NvApp\DarkModeCheck.exe",
            @"NvApp\InstallerExtension.dll",
            @"NvApp\NvApp.nvi",
            @"NvApp\NvAppApi.dll",
            @"NvApp\NvAppExt.dll",
            @"NvApp\NvConfigGenerator.dll",
        ],
        TurnOffInConfig: [],
        Setup: "setup.exe",
        SetupArguments: ["-s", "-noreboot", "-noeula", "-clean"]);

    // AMD's full package: nothing is deleted, the extras are switched off in its own config
    // (Graphics.ps1:582-608) and the setup runs without its wizard.
    private static readonly DebloatPlan Amd = new(
        RemoveAfterExtracting: [],
        TurnOffInConfig:
        [
            @"Config\AMDAUEPInstaller.xml",
            @"Config\AMDCOMPUTE.xml",
            @"Config\AMDLinkDriverUpdate.xml",
            @"Config\AMDRELAUNCHER.xml",
            @"Config\AMDScoSupportTypeUpdate.xml",
            @"Config\AMDUpdater.xml",
            @"Config\AMDUWPLauncher.xml",
            @"Config\EnableWindowsDriverSearch.xml",
            @"Config\InstallUEP.xml",
            @"Config\ModifyLinkUpdate.xml",
            @"Config\InstallManifest.json",
            @"Bin64\cccmanifest_64.json",
        ],
        Setup: @"Bin64\ATISetup.exe",
        SetupArguments: ["-INSTALL", "-VIEW:2"]);

    // Intel's package strips itself: its installer has a no-extras switch (Graphics.ps1:663).
    private static readonly DebloatPlan Intel = new(
        RemoveAfterExtracting: [],
        TurnOffInConfig: [],
        Setup: "Installer.exe",
        SetupArguments: ["-f", "--noExtras", "--terminateProcesses", "-s"],
        AfterSetup: @"Resources\Extras\IntelGraphicsSoftware_*.exe",
        AfterSetupArguments: ["/s"]);

    /// <summary>A file name each vendor's own installer has, as the rows describe it.</summary>
    public static string InstallerNameFor(GpuVendor vendor) => vendor switch
    {
        GpuVendor.Nvidia => "617.42-desktop-win10-win11-64bit-international-dch-whql.exe",
        GpuVendor.Amd => "whql-amd-software-adrenalin-edition-26.8.1-win10-win11-aug2026.exe",
        GpuVendor.Intel => "gfx_win_101.6083.exe",
        _ => throw new ArgumentOutOfRangeException(nameof(vendor), vendor, null),
    };

    /// <summary>The plan for this vendor's installer, or <c>null</c> when the file is not one.</summary>
    public static DebloatPlan? Plan(GpuVendor vendor, string installerFileName) => vendor switch
    {
        GpuVendor.Nvidia when NvidiaInstaller().IsMatch(installerFileName) => Nvidia,
        GpuVendor.Amd when AmdInstaller().IsMatch(installerFileName) => Amd,
        GpuVendor.Intel when IntelInstaller().IsMatch(installerFileName) => Intel,
        _ => null,
    };

    /// <summary>
    /// A config file's text with its components switched off: AMD's XML entries are disabled and
    /// unhidden, and its JSON manifests stop installing by default (Graphics.ps1:594-608).
    /// </summary>
    public static string TurnOff(string fileName, string content) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".xml" => content
                .Replace("<Enabled>true</Enabled>", "<Enabled>false</Enabled>", StringComparison.Ordinal)
                .Replace("<Hidden>true</Hidden>", "<Hidden>false</Hidden>", StringComparison.Ordinal),
            ".json" => InstallByDefaultYes().Replace(content, "\"InstallByDefault\" : \"No\""),
            _ => content,
        };

    // The full package, not the minimalsetup web installer, which has nothing in it to strip.
    [GeneratedRegex(@"^[0-9]+\.[0-9]+-.*-dch-whql\.exe$", RegexOptions.IgnoreCase)]
    private static partial Regex NvidiaInstaller();

    [GeneratedRegex(@"^(whql-)?amd-software-adrenalin-edition-[0-9.]+-(?!minimalsetup).*\.exe$", RegexOptions.IgnoreCase)]
    private static partial Regex AmdInstaller();

    [GeneratedRegex(@"^gfx_win_[0-9.]+\.exe$", RegexOptions.IgnoreCase)]
    private static partial Regex IntelInstaller();

    [GeneratedRegex(@"""InstallByDefault""\s*:\s*""Yes""")]
    private static partial Regex InstallByDefaultYes();
}
