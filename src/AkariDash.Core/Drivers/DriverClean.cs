namespace AkariDash.Core.Drivers;

/// <summary>
/// How a DDU clean runs, as AkariOS's driver-clean entry runs it (Graphics.ps1:3-186): DDU from the
/// AkariOS-Files mirror, unpacked with 7-Zip, given AkariOS's settings, Windows Update kept from
/// searching for drivers, then safe mode and a restart, where DDU cleans every GPU and the
/// SoundBlaster and Realtek drivers. The one difference: the safe-mode half is a small exe beside
/// the app (ADR-0001), not a .ps1 run through RunOnce with the execution policy bypassed.
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

    /// <summary>
    /// DDU's arguments for the automatic clean (Graphics.ps1:84), without <c>-Restart</c>: the
    /// companion restarts once it has written down how the clean went.
    /// </summary>
    public static IReadOnlyList<string> Arguments { get; } = ["-CleanSoundBlaster", "-CleanRealtek", "-CleanAllGpus"];

    /// <summary>
    /// What AkariOS writes before the restart so Windows Update does not put a driver back
    /// (Graphics.ps1:73 and 167): HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching.
    /// </summary>
    public static DriverSetting SearchOrderConfig { get; } = new(
        DriverSettingRoot.HklmSoftware, @"Microsoft\Windows\CurrentVersion\DriverSearching", "SearchOrderConfig", DriverSettingKind.Dword, "0");

    /// <summary>The RunOnce command line that starts the companion with the run's state file.</summary>
    public static string RunOnceCommand(string companionPath, string statePath) =>
        $"\"{companionPath}\" --state \"{statePath}\"";

    /// <summary>DDU's settings (Graphics.ps1:29-66), verbatim.</summary>
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

/// <summary>7-Zip, which AkariOS installs from its mirror to unpack DDU, DirectX and the drivers.</summary>
public static class SevenZip
{
    public static Uri Download { get; } = new("https://github.com/isleap9/AkariOS-Files/releases/download/Files/7zip.exe");

    public const string Program = @"%ProgramFiles%\7-Zip\7z.exe";

    /// <summary>The arguments that unpack <paramref name="archive"/> into <paramref name="folder"/> (AkariOS's <c>7z x ... -o... -y</c>).</summary>
    public static IReadOnlyList<string> Extract(string archive, string folder) => ["x", archive, $"-o{folder}", "-y"];

    /// <summary>What AkariOS does around the install (Graphics.ps1:8-20): two menu settings, and its Start menu folder tidied away.</summary>
    public static IReadOnlyList<DriverExtra> AfterInstall { get; } =
    [
        new SetRegistry(null, new DriverSetting(DriverSettingRoot.Hkcu, @"Software\7-Zip\Options", "ContextMenu", DriverSettingKind.Dword, "259")),
        new SetRegistry(null, new DriverSetting(DriverSettingRoot.Hkcu, @"Software\7-Zip\Options", "CascadedMenu", DriverSettingKind.Dword, "0")),
        new MoveFile(@"%ProgramData%\Microsoft\Windows\Start Menu\Programs\7-Zip\7-Zip File Manager.lnk", @"%ProgramData%\Microsoft\Windows\Start Menu\Programs"),
        new RemovePath(@"%ProgramData%\Microsoft\Windows\Start Menu\Programs\7-Zip"),
    ];
}
