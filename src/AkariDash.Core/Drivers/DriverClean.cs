using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>
/// How a DDU clean is run, as data: where DDU comes from, the settings it is given, the command
/// line it runs in safe mode, and the RunOnce line that starts the companion there. Transcribed
/// from AkariOS's driver-clean entry (Graphics.ps1:3-186), with two deliberate differences: DDU
/// cleans only the card the person picked, never <c>-CleanAllGpus</c> or the sound drivers, and
/// nothing is written that outlives the run (AkariOS also sets Windows Update's driver search off
/// for good; that is a Tweak, not part of an Action).
/// </summary>
public static class DriverClean
{
    /// <summary>DDU, from the Akari family's own mirror, the copy AkariOS uses (Graphics.ps1:23).</summary>
    public static Uri DduDownload { get; } = new("https://github.com/isleap9/AkariOS-Files/releases/download/Files/ddu.exe");

    /// <summary>The program inside DDU's folder.</summary>
    public const string DduExecutable = "Display Driver Uninstaller.exe";

    /// <summary>Where DDU reads its settings from, relative to its folder.</summary>
    public const string DduSettingsFile = @"Settings\Settings.xml";

    /// <summary>The companion exe that runs in safe mode, beside the app.</summary>
    public const string CompanionExecutable = "AkariDash.DriverReset.exe";

    /// <summary>
    /// The RunOnce value's name. The leading <c>*</c> is what makes Windows run it in safe mode
    /// too; without it, safe mode skips RunOnce entirely.
    /// </summary>
    public const string RunOnceValueName = "*AkariDashDriverReset";

    /// <summary>DDU's own arguments for an unattended clean of one vendor's driver. No restart: the companion restarts once it has written down how the clean went.</summary>
    public static IReadOnlyList<string> Arguments(GpuVendor vendor) => vendor switch
    {
        GpuVendor.Nvidia => ["-CleanNvidia"],
        GpuVendor.Amd => ["-CleanAmd"],
        GpuVendor.Intel => ["-CleanIntel"],
        _ => throw new ArgumentOutOfRangeException(nameof(vendor), vendor, null),
    };

    /// <summary>The RunOnce command line that starts the companion with the run's state file.</summary>
    public static string RunOnceCommand(string companionPath, string statePath) =>
        $"\"{companionPath}\" --state \"{statePath}\"";

    /// <summary>
    /// DDU's settings (Graphics.ps1:29-66): English, no update check, no offer, no restore point,
    /// no safe-mode dialog of its own (we handle safe mode), and Windows Update kept from putting a
    /// driver back while DDU works.
    /// </summary>
    public const string DduSettingsXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <DisplayDriverUninstaller Version="18.1.4.2">
        	<Settings>
        		<SelectedLanguage>en-US</SelectedLanguage>
        		<RemoveMonitors>True</RemoveMonitors>
        		<RemoveCrimsonCache>True</RemoveCrimsonCache>
        		<RemoveAMDDirs>True</RemoveAMDDirs>
        		<RemoveAudioBus>True</RemoveAudioBus>
        		<RemoveAMDKMPFD>True</RemoveAMDKMPFD>
        		<RemoveNvidiaDirs>True</RemoveNvidiaDirs>
        		<RemovePhysX>True</RemovePhysX>
        		<Remove3DTVPlay>True</Remove3DTVPlay>
        		<RemoveGFE>True</RemoveGFE>
        		<RemoveNVBROADCAST>True</RemoveNVBROADCAST>
        		<RemoveNVCP>True</RemoveNVCP>
        		<RemoveINTELCP>True</RemoveINTELCP>
        		<RemoveINTELIGS>True</RemoveINTELIGS>
        		<RemoveOneAPI>True</RemoveOneAPI>
        		<RemoveEnduranceGaming>True</RemoveEnduranceGaming>
        		<RemoveIntelNpu>True</RemoveIntelNpu>
        		<RemoveAMDCP>True</RemoveAMDCP>
        		<UseRoamingConfig>False</UseRoamingConfig>
        		<CheckUpdates>False</CheckUpdates>
        		<CreateRestorePoint>False</CreateRestorePoint>
        		<SaveLogs>False</SaveLogs>
        		<RemoveVulkan>True</RemoveVulkan>
        		<ShowOffer>False</ShowOffer>
        		<EnableSafeModeDialog>False</EnableSafeModeDialog>
        		<PreventWinUpdate>True</PreventWinUpdate>
        		<UsedBCD>False</UsedBCD>
        		<KeepNVCPopt>False</KeepNVCPopt>
        		<RememberLastChoice>False</RememberLastChoice>
        		<LastSelectedGPUIndex>0</LastSelectedGPUIndex>
        		<LastSelectedTypeIndex>0</LastSelectedTypeIndex>
        	</Settings>
        </DisplayDriverUninstaller>
        """;
}
