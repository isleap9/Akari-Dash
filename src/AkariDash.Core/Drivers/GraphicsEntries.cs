using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>How an entry is shown and pressed, as AkariOS's Add-Tweak -Kind.</summary>
public enum GraphicsEntryKind
{
    /// <summary>Optimize and Default buttons, with a dot that says which was pressed last.</summary>
    Toggle,

    /// <summary>One button that runs it.</summary>
    Action,

    /// <summary>An Options button that opens one row per choice, each with its own button.</summary>
    Group,
}

/// <summary>One choice inside a Group entry, e.g. "NVIDIA" under "Driver install (updated)".</summary>
/// <param name="Kind">The driver work it does.</param>
/// <param name="Vendor">The card it is for; <see langword="null"/> for DDU, which cleans them all.</param>
/// <param name="Confirm">The question asked first, when AkariOS asks one.</param>
public sealed record GraphicsOption(
    string Name,
    string Description,
    string Button,
    DriverStepKind Kind,
    GpuVendor? Vendor,
    string? Confirm = null);

/// <summary>One row of the Graphics list, named, described and rated as AkariOS has it.</summary>
/// <param name="Vendor">The card a Toggle is for; <see langword="null"/> when it is for any.</param>
public sealed record GraphicsEntry(
    string Id,
    string Name,
    string Description,
    DriverRisk Risk,
    GraphicsEntryKind Kind,
    string Button = "Run",
    IReadOnlyList<GraphicsOption>? Options = null,
    GpuVendor? Vendor = null)
{
    public IReadOnlyList<GraphicsOption> Choices => Options ?? [];
}

/// <summary>
/// AkariOS-Ultimate's Graphics category, entry for entry and in its order (Tweaks/Graphics.ps1):
/// the names, descriptions, risks and buttons are AkariOS's own, and so is what each one does.
/// </summary>
public static class GraphicsEntries
{
    private static readonly Uri Files = new("https://github.com/isleap9/AkariOS-Files/releases/download/Files/");

    private const string ProfileInspectorFolder = @"%ProgramFiles(x86)%\Nvidia Profile Inspector";

    private const string NvTweak = @"nvlddmkm\Parameters\Global\NVTweak";

    public static IReadOnlyList<GraphicsEntry> All { get; } =
    [
        new("driver-clean", "Driver clean (DDU)", "Remove the current graphics driver with DDU", DriverRisk.Advanced, GraphicsEntryKind.Group, Options:
        [
            new("DDU: Auto", "Run DDU automatically", "Run", DriverStepKind.Clean, null, "DDU will remove your graphics driver and restart the PC. Continue?"),
            new("DDU: Manual", "Open DDU to clean manually", "Run", DriverStepKind.CleanManual, null, "DDU will remove your graphics driver. Continue?"),
        ]),
        new("driver-install", "Driver install (updated)", "Install the latest graphics driver", DriverRisk.Caution, GraphicsEntryKind.Group, Options:
        [
            new("NVIDIA", "Driver install (updated): NVIDIA", "Install", DriverStepKind.Install, GpuVendor.Nvidia),
            new("AMD", "Driver install (updated): AMD", "Install", DriverStepKind.Install, GpuVendor.Amd),
            new("INTEL", "Driver install (updated): INTEL", "Install", DriverStepKind.Install, GpuVendor.Intel),
        ]),
        new("driver-install-settings", "Driver install and settings (updated)", "Install the latest graphics driver and import optimized settings", DriverRisk.Caution, GraphicsEntryKind.Group, Options:
        [
            new("NVIDIA", "Download, install, and configure the latest NVIDIA driver", "Install", DriverStepKind.InstallAndConfigure, GpuVendor.Nvidia),
            new("AMD", "Download, install, and configure the latest AMD driver", "Install", DriverStepKind.InstallAndConfigure, GpuVendor.Amd),
            new("Intel", "Open Intel driver download page, select downloaded driver, install and configure", "Install", DriverStepKind.InstallAndConfigure, GpuVendor.Intel),
        ]),
        new("driver-debloat", "Driver debloat install and settings", "Install a debloated graphics driver and import optimized settings", DriverRisk.Caution, GraphicsEntryKind.Group, Options:
        [
            new("NVIDIA", "Select a downloaded NVIDIA driver, debloat it, install and configure", "Install", DriverStepKind.Debloat, GpuVendor.Nvidia),
            new("AMD", "Select a downloaded AMD driver, debloat it, install and configure", "Install", DriverStepKind.Debloat, GpuVendor.Amd),
            new("Intel", "Select a downloaded Intel driver, debloat it, install and configure", "Install", DriverStepKind.Debloat, GpuVendor.Intel),
        ]),
        new("nvidia-settings", "NVIDIA settings", "Apply optimized NVIDIA driver settings", DriverRisk.Caution, GraphicsEntryKind.Toggle, Vendor: GpuVendor.Nvidia),
        new("amd-settings", "AMD settings", "Apply optimized AMD driver settings", DriverRisk.Caution, GraphicsEntryKind.Toggle, Vendor: GpuVendor.Amd),
        new("intel-settings", "Intel settings", "Apply optimized Intel graphics settings", DriverRisk.Caution, GraphicsEntryKind.Toggle, Vendor: GpuVendor.Intel),
        new("hdcp", "HDCP", "Turn NVIDIA HDCP off", DriverRisk.Caution, GraphicsEntryKind.Toggle, Vendor: GpuVendor.Nvidia),
        new("p0-state", "P0 state", "Keep the NVIDIA GPU at its highest performance power state", DriverRisk.Caution, GraphicsEntryKind.Toggle, Vendor: GpuVendor.Nvidia),
        new("msi-mode", "MSI mode", "Turn MSI mode on for the GPU (Default turns it off)", DriverRisk.Caution, GraphicsEntryKind.Toggle),
        new("directx", "DirectX", "Download and install DirectX", DriverRisk.Safe, GraphicsEntryKind.Action, Button: "Install"),
        new("cpp", "C++ redistributables", "Download and install all C++ redistributables", DriverRisk.Safe, GraphicsEntryKind.Action, Button: "Install"),
        new("resolution", "Resolution and refresh rate", "Open the display settings", DriverRisk.Safe, GraphicsEntryKind.Action, Button: "Open"),
        new("hags-windowed", "HAGS and windowed optimizations", "Open the advanced graphics settings", DriverRisk.Safe, GraphicsEntryKind.Action, Button: "Open"),
    ];

    /// <summary>What Optimize (a Toggle) or the button (an Action) does, in AkariOS's order.</summary>
    public static IReadOnlyList<DriverExtra> Apply(string id) => id switch
    {
        "nvidia-settings" => NvidiaSettings(optimize: true),
        "amd-settings" => AmdSettingsApply,
        "intel-settings" => IntelSettingsApply,
        "hdcp" => [Adapter(GpuVendor.Nvidia, "", "RMHdcpKeyglobZero", "1")],
        "p0-state" => [Adapter(GpuVendor.Nvidia, "", "DisableDynamicPstate", "1")],
        "msi-mode" => [new SetMsiMode(1)],
        "directx" => [new EnsureSevenZip(), new InstallDirectX(new Uri(Files, "directx.exe"))],
        "cpp" => Redistributables,
        "resolution" => [new OpenTarget("ms-settings:display")],
        "hags-windowed" => [new OpenTarget("ms-settings:display-advancedgraphics")],
        _ => [],
    };

    /// <summary>What Default does, in AkariOS's order; empty for an entry that has no Default.</summary>
    public static IReadOnlyList<DriverExtra> Revert(string id) => id switch
    {
        "nvidia-settings" => NvidiaSettings(optimize: false),
        "amd-settings" => AmdSettingsRevert,
        "intel-settings" => [new DeleteRegistry(GpuVendor.Intel, DriverSettingRoot.GraphicsClass, "3DKeys")],
        "hdcp" => [Adapter(GpuVendor.Nvidia, "", "RMHdcpKeyglobZero", "0")],
        "p0-state" => [Adapter(GpuVendor.Nvidia, "", "DisableDynamicPstate", "0")],
        "msi-mode" => [new SetMsiMode(0)],
        _ => [],
    };

    private static SetRegistry Adapter(GpuVendor vendor, string keyPath, string name, string value, DriverSettingKind kind = DriverSettingKind.Dword) =>
        new(vendor, new DriverSetting(DriverSettingRoot.GraphicsClass, keyPath, name, kind, value));

    private static SetRegistry User(string keyPath, string name, DriverSettingKind kind, string value) =>
        new(null, new DriverSetting(DriverSettingRoot.Hkcu, keyPath, name, kind, value));

    private static SetRegistry Service(string keyPath, string name, string value) =>
        new(null, new DriverSetting(DriverSettingRoot.HklmServices, keyPath, name, DriverSettingKind.Dword, value));

    // Graphics.ps1:766-1119. Default puts back what Optimize changed, then reinstalls the same tools
    // and imports an empty Base Profile, as AkariOS's Revert does.
    private static IReadOnlyList<DriverExtra> NvidiaSettings(bool optimize) =>
    [
        new InstallAppx("the NVIDIA Control Panel", new Uri(Files, "nvp.appx")),
        new CreateShortcut("NVIDIA Control Panel", DriverExtras.NvidiaControlPanelAppId, "shell:appsFolder"),
        new UnblockFiles(@"%ProgramData%\NVIDIA Corporation\Drs"),
        .. optimize
            ? new DriverExtra[]
            {
                Service(NvTweak, "NvCplPhysxAuto", "0"),
                Service(NvTweak, "NvDevToolsVisible", "1"),
                Adapter(GpuVendor.Nvidia, "", "RmProfilingAdminOnly", "0"),
                Service(NvTweak, "RmProfilingAdminOnly", "0"),
                User(@"Software\NVIDIA Corporation\NvTray", "StartOnLogin", DriverSettingKind.Dword, "0"),
                Service(@"nvlddmkm\FTS", "EnableGR535", "0"),
                Service(@"nvlddmkm\Parameters\FTS", "EnableGR535", "0"),
            }
            : new DriverExtra[]
            {
                new DeleteRegistry(null, DriverSettingRoot.HklmServices, NvTweak, "NvCplPhysxAuto"),
                new DeleteRegistry(null, DriverSettingRoot.HklmServices, NvTweak, "NvDevToolsVisible"),
                new DeleteRegistry(GpuVendor.Nvidia, DriverSettingRoot.GraphicsClass, "", "RmProfilingAdminOnly"),
                new DeleteRegistry(null, DriverSettingRoot.HklmServices, NvTweak, "RmProfilingAdminOnly"),
                new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\NVIDIA Corporation\NvTray"),
                Service(@"nvlddmkm\FTS", "EnableGR535", "1"),
                Service(@"nvlddmkm\Parameters\FTS", "EnableGR535", "1"),
            },
        new DownloadTool("Nvidia Profile Inspector", new Uri(Files, "inspector.exe"), DriverExtras.ProfileInspector),
        new CreateShortcut("Nvidia Profile Inspector", DriverExtras.ProfileInspector, ProfileInspectorFolder),
        new CreateShortcut("Nvidia Profile Inspector", DriverExtras.ProfileInspector, ProfileInspectorFolder, StartMenu: true),
        new ImportNvidiaProfile(DriverExtras.ProfileInspector, Reset: !optimize),
        new OpenTarget(DriverExtras.NvidiaControlPanelAppId),
    ];

    // Graphics.ps1:1120-1310.
    private static readonly IReadOnlyList<DriverExtra> AmdSettingsApply =
    [
        new OpenThenClose(@"%SystemDrive%\Program Files\AMD\CNext\CNext\RadeonSoftware.exe", "RadeonSoftware", 30),
        User(@"Software\AMD\CN", "AutoUpdate", DriverSettingKind.Dword, "0"),
        User(@"Software\AMD\AIM", "LaunchBugTool", DriverSettingKind.Dword, "0"),
        User(@"Software\AMD\DVR", "HotkeysDisabled", DriverSettingKind.Dword, "1"),
        User(@"Software\AMD\CN", "SystemTray", DriverSettingKind.String, "false"),
        User(@"Software\AMD\DVR", "ShowRSOverlay", DriverSettingKind.String, "false"),
        User(@"Software\AMD\CN", "RSXBrowserUnavailable", DriverSettingKind.String, "true"),
        User(@"Software\AMD\CN", "AllowWebContent", DriverSettingKind.String, "false"),
        User(@"Software\AMD\CN", "CN_Hide_Toast_Notification", DriverSettingKind.String, "true"),
        User(@"Software\AMD\CN", "AnimationEffect", DriverSettingKind.String, "false"),
        User(@"Software\AMD\CN", "WizardProfile", DriverSettingKind.String, "PROFILE_CUSTOM"),
        Adapter(GpuVendor.Amd, "", "KMD_DeLagEnabled", "1"),
        Adapter(GpuVendor.Amd, "UMD", "VSyncControl", "3000", DriverSettingKind.Binary),
        Adapter(GpuVendor.Amd, "UMD", "TFQ", "3200", DriverSettingKind.Binary),
        Adapter(GpuVendor.Amd, "UMD", "Tessellation", "3100", DriverSettingKind.Binary),
        Adapter(GpuVendor.Amd, "UMD", "Tessellation_OPTION", "3200", DriverSettingKind.Binary),
        User(@"Software\AMD\CN\CustomResolutions", "EulaAccepted", DriverSettingKind.String, "true"),
        User(@"Software\AMD\CN\DisplayOverride", "EulaAccepted", DriverSettingKind.String, "true"),
        Adapter(GpuVendor.Amd, "power_v1", "abmlevel", "00000000", DriverSettingKind.Binary),
        Adapter(GpuVendor.Amd, "", "IsAutoDefault", "00000000", DriverSettingKind.Binary),
        Adapter(GpuVendor.Amd, "", "IsComponentControl", "0f000000", DriverSettingKind.Binary),
        new ClearUserKey(@"Software\AMD\CN\Notification"),
        User(@"Software\AMD\CN\FreeSync", "AlreadyNotified", DriverSettingKind.Dword, "1"),
        User(@"Software\AMD\CN\OverlayNotification", "AlreadyNotified", DriverSettingKind.Dword, "1"),
        User(@"Software\AMD\CN\VirtualSuperResolution", "AlreadyNotified", DriverSettingKind.Dword, "1"),
    ];

    // Graphics.ps1:1311-1372.
    private static readonly IReadOnlyList<DriverExtra> AmdSettingsRevert =
    [
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\CN", "AutoUpdate"),
        User(@"Software\AMD\AIM", "LaunchBugTool", DriverSettingKind.Dword, "1"),
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\DVR", "HotkeysDisabled"),
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\CN", "SystemTray"),
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\DVR", "ShowRSOverlay"),
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\CN", "RSXBrowserUnavailable"),
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\CN", "AllowWebContent"),
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\CN", "CN_Hide_Toast_Notification"),
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\CN", "AnimationEffect"),
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\CN", "WizardProfile"),
        Adapter(GpuVendor.Amd, "", "KMD_DeLagEnabled", "0"),
        Adapter(GpuVendor.Amd, "UMD", "VSyncControl", "31000000", DriverSettingKind.Binary),
        new DeleteRegistry(GpuVendor.Amd, DriverSettingRoot.GraphicsClass, "UMD", "TFQ"),
        Adapter(GpuVendor.Amd, "UMD", "Tessellation", "360034000000", DriverSettingKind.Binary),
        Adapter(GpuVendor.Amd, "UMD", "Tessellation_OPTION", "30000000", DriverSettingKind.Binary),
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\CN\CustomResolutions"),
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\CN\DisplayOverride"),
        new DeleteRegistry(GpuVendor.Amd, DriverSettingRoot.GraphicsClass, "power_v1", "abmlevel"),
        Adapter(GpuVendor.Amd, "", "IsAutoDefault", "1"),
        Adapter(GpuVendor.Amd, "", "IsComponentControl", "00000000", DriverSettingKind.Binary),
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\CN\Notification"),
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\CN\FreeSync"),
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\CN\OverlayNotification"),
        new DeleteRegistry(null, DriverSettingRoot.Hkcu, @"Software\AMD\CN\VirtualSuperResolution"),
    ];

    // Graphics.ps1:1374-1403.
    private static readonly IReadOnlyList<DriverExtra> IntelSettingsApply =
    [
        Adapter(GpuVendor.Intel, "3DKeys", "Global_AsyncFlipMode", "2"),
        Adapter(GpuVendor.Intel, "3DKeys", "Global_LowLatency", "1"),
        User(@"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings", DriverSettingKind.String, "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;"),
    ];

    // Graphics.ps1:1610-1645: every runtime, each with the switches AkariOS passes it.
    private static IReadOnlyList<DriverExtra> Redistributables =>
    [
        Redist("vcredist2005_x86.exe", "/Q /C:\"msiexec /i vcredist.msi /qn /norestart\""),
        Redist("vcredist2005_x64.exe", "/Q /C:\"msiexec /i vcredist.msi /qn /norestart\""),
        Redist("vcredist2008_x86.exe", "/q"),
        Redist("vcredist2008_x64.exe", "/q"),
        Redist("vcredist2010_x86.exe", "/quiet /norestart"),
        Redist("vcredist2010_x64.exe", "/quiet /norestart"),
        Redist("vcredist2012_x86.exe", "/quiet /norestart"),
        Redist("vcredist2012_x64.exe", "/quiet /norestart"),
        Redist("vcredist2013_x86.exe", "/quiet /norestart"),
        Redist("vcredist2013_x64.exe", "/quiet /norestart"),
        Redist("vcredist2015_2017_2019_2022_x86.exe", "/quiet /norestart"),
        Redist("vcredist2015_2017_2019_2022_x64.exe", "/quiet /norestart"),
    ];

    private static InstallRedistributable Redist(string file, string arguments) =>
        new(Path.GetFileNameWithoutExtension(file), new Uri(Files, file), arguments);
}
