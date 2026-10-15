using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>Which of the three places a driver setting is written below.</summary>
public enum DriverSettingRoot
{
    /// <summary>Below <c>HKEY_CURRENT_USER</c>.</summary>
    Hkcu,

    /// <summary>Below <c>HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services</c>.</summary>
    HklmServices,

    /// <summary>
    /// Below each of this vendor's adapter keys under the Display class,
    /// <c>HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\NNNN</c>.
    /// An empty key path is the adapter key itself.
    /// </summary>
    GraphicsClass,

    /// <summary>Below <c>HKEY_LOCAL_MACHINE\SOFTWARE</c>.</summary>
    HklmSoftware,
}

public enum DriverSettingKind
{
    Dword,
    String,
    Binary,
}

/// <summary>
/// One value a configured install writes. <see cref="Value"/> is the data as the kind declares it:
/// decimal digits for a Dword, the literal text for a String, whole hex bytes for a Binary.
/// </summary>
public sealed record DriverSetting(
    DriverSettingRoot Root,
    string KeyPath,
    string Name,
    DriverSettingKind Kind,
    string Value);

/// <summary>
/// The settings bundle "Install and configure" puts on with the driver, transcribed from
/// AkariOS's driver-install-settings entry. Defined once here: the GPUs page (#33) offers the same
/// values as toggles and must read them from here, not from a copy. Paths use CurrentControlSet;
/// AkariOS's ControlSet001 is only the set that happened to be current where it was written.
/// </summary>
public static class DriverSettings
{
    private static readonly IReadOnlyList<DriverSetting> Nvidia =
    [
        // Graphics.ps1:280-294, in AkariOS's order: P0 state and HDCP off on the adapter, then the rest.
        new(DriverSettingRoot.GraphicsClass, "", "DisableDynamicPstate", DriverSettingKind.Dword, "1"),
        new(DriverSettingRoot.GraphicsClass, "", "RMHdcpKeyglobZero", DriverSettingKind.Dword, "1"),
        new(DriverSettingRoot.HklmServices, @"nvlddmkm\Parameters\Global\NVTweak", "NvCplPhysxAuto", DriverSettingKind.Dword, "0"),
        new(DriverSettingRoot.HklmServices, @"nvlddmkm\Parameters\Global\NVTweak", "NvDevToolsVisible", DriverSettingKind.Dword, "1"),
        new(DriverSettingRoot.GraphicsClass, "", "RmProfilingAdminOnly", DriverSettingKind.Dword, "0"),
        new(DriverSettingRoot.HklmServices, @"nvlddmkm\Parameters\Global\NVTweak", "RmProfilingAdminOnly", DriverSettingKind.Dword, "0"),
        new(DriverSettingRoot.Hkcu, @"Software\NVIDIA Corporation\NvTray", "StartOnLogin", DriverSettingKind.Dword, "0"),
        new(DriverSettingRoot.HklmServices, @"nvlddmkm\FTS", "EnableGR535", DriverSettingKind.Dword, "0"),
        new(DriverSettingRoot.HklmServices, @"nvlddmkm\Parameters\FTS", "EnableGR535", DriverSettingKind.Dword, "0"),
    ];

    private static readonly IReadOnlyList<DriverSetting> Amd =
    [
        // Graphics.ps1:382-412
        new(DriverSettingRoot.Hkcu, @"Software\AMD\CN", "AutoUpdate", DriverSettingKind.Dword, "0"),
        new(DriverSettingRoot.Hkcu, @"Software\AMD\CN", "WizardProfile", DriverSettingKind.String, "PROFILE_CUSTOM"),
        new(DriverSettingRoot.Hkcu, @"Software\AMD\CN", "SystemTray", DriverSettingKind.String, "false"),
        new(DriverSettingRoot.Hkcu, @"Software\AMD\CN", "CN_Hide_Toast_Notification", DriverSettingKind.String, "true"),
        new(DriverSettingRoot.Hkcu, @"Software\AMD\CN", "AnimationEffect", DriverSettingKind.String, "false"),
        new(DriverSettingRoot.Hkcu, @"Software\AMD\CN\CustomResolutions", "EulaAccepted", DriverSettingKind.String, "true"),
        new(DriverSettingRoot.Hkcu, @"Software\AMD\CN\DisplayOverride", "EulaAccepted", DriverSettingKind.String, "true"),
        new(DriverSettingRoot.Hkcu, @"Software\AMD\CN\FreeSync", "AlreadyNotified", DriverSettingKind.Dword, "1"),
        new(DriverSettingRoot.Hkcu, @"Software\AMD\CN\OverlayNotification", "AlreadyNotified", DriverSettingKind.Dword, "1"),
        new(DriverSettingRoot.Hkcu, @"Software\AMD\CN\VirtualSuperResolution", "AlreadyNotified", DriverSettingKind.Dword, "1"),
        new(DriverSettingRoot.GraphicsClass, "", "KMD_DeLagEnabled", DriverSettingKind.Dword, "1"),
        new(DriverSettingRoot.GraphicsClass, "UMD", "VSyncControl", DriverSettingKind.Binary, "3000"),
        new(DriverSettingRoot.GraphicsClass, "UMD", "TFQ", DriverSettingKind.Binary, "3200"),
        new(DriverSettingRoot.GraphicsClass, "UMD", "Tessellation", DriverSettingKind.Binary, "3100"),
        new(DriverSettingRoot.GraphicsClass, "UMD", "Tessellation_OPTION", DriverSettingKind.Binary, "3200"),
        new(DriverSettingRoot.GraphicsClass, "power_v1", "abmlevel", DriverSettingKind.Binary, "00000000"),
    ];

    private static readonly IReadOnlyList<DriverSetting> Intel =
    [
        // Graphics.ps1:1376-1403
        new(DriverSettingRoot.GraphicsClass, "3DKeys", "Global_AsyncFlipMode", DriverSettingKind.Dword, "2"),
        new(DriverSettingRoot.GraphicsClass, "3DKeys", "Global_LowLatency", DriverSettingKind.Dword, "1"),
        new(DriverSettingRoot.Hkcu, @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings", DriverSettingKind.String, "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;"),
    ];

    /// <summary>The empty Base Profile NVIDIA settings' Default imports (Graphics.ps1:1103-1111), verbatim.</summary>
    public const string NvidiaEmptyProfileXml = """
        <?xml version="1.0" encoding="utf-16"?>
        <ArrayOfProfile>
          <Profile>
            <ProfileName>Base Profile</ProfileName>
            <Executeables/>
            <Settings/>
          </Profile>
        </ArrayOfProfile>
        """;

    public static IReadOnlyList<DriverSetting> For(GpuVendor vendor) => vendor switch
    {
        GpuVendor.Nvidia => Nvidia,
        GpuVendor.Amd => Amd,
        GpuVendor.Intel => Intel,
        _ => [],
    };

    /// <summary>
    /// NVIDIA's Base Profile (Graphics.ps1:308-347), imported with Nvidia Profile Inspector,
    /// verbatim - the "Preferred OpenGL GPU" line included, as AkariOS imports it.
    /// </summary>
    public const string NvidiaProfileXml = """
        <?xml version="1.0" encoding="utf-16"?>
        <ArrayOfProfile>
          <Profile>
            <ProfileName>Base Profile</ProfileName>
            <Executables/>
            <Settings>
              <ProfileSetting><SettingNameInfo>Frame Rate Limiter V3</SettingNameInfo><SettingID>277041154</SettingID><SettingValue>0</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>GSYNC - Application Mode</SettingNameInfo><SettingID>294973784</SettingID><SettingValue>0</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>GSYNC - Application State</SettingNameInfo><SettingID>279476687</SettingID><SettingValue>4</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>GSYNC - Global Feature</SettingNameInfo><SettingID>278196567</SettingID><SettingValue>0</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>GSYNC - Global Mode</SettingNameInfo><SettingID>278196727</SettingID><SettingValue>0</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>GSYNC - Indicator Overlay</SettingNameInfo><SettingID>268604728</SettingID><SettingValue>0</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Maximum Pre-Rendered Frames</SettingNameInfo><SettingID>8102046</SettingID><SettingValue>1</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Preferred Refresh Rate</SettingNameInfo><SettingID>6600001</SettingID><SettingValue>1</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Ultra Low Latency - CPL State</SettingNameInfo><SettingID>390467</SettingID><SettingValue>2</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Ultra Low Latency - Enabled</SettingNameInfo><SettingID>277041152</SettingID><SettingValue>1</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Vertical Sync</SettingNameInfo><SettingID>11041231</SettingID><SettingValue>138504007</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Vertical Sync - Smooth AFR Behavior</SettingNameInfo><SettingID>270198627</SettingID><SettingValue>0</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Vertical Sync - Tear Control</SettingNameInfo><SettingID>5912412</SettingID><SettingValue>2525368439</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Vulkan/OpenGL Present Method</SettingNameInfo><SettingID>550932728</SettingID><SettingValue>0</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Antialiasing - Gamma Correction</SettingNameInfo><SettingID>276652957</SettingID><SettingValue>0</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Antialiasing - Mode</SettingNameInfo><SettingID>276757595</SettingID><SettingValue>1</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Antialiasing - Setting</SettingNameInfo><SettingID>282555346</SettingID><SettingValue>0</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Anisotropic Filter - Optimization</SettingNameInfo><SettingID>8703344</SettingID><SettingValue>1</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Anisotropic Filter - Sample Optimization</SettingNameInfo><SettingID>15151633</SettingID><SettingValue>1</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Anisotropic Filtering - Mode</SettingNameInfo><SettingID>282245910</SettingID><SettingValue>1</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Anisotropic Filtering - Setting</SettingNameInfo><SettingID>270426537</SettingID><SettingValue>1</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Texture Filtering - Negative LOD Bias</SettingNameInfo><SettingID>1686376</SettingID><SettingValue>0</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Texture Filtering - Quality</SettingNameInfo><SettingID>13510289</SettingID><SettingValue>20</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Texture Filtering - Trilinear Optimization</SettingNameInfo><SettingID>3066610</SettingID><SettingValue>0</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>CUDA - Force P2 State</SettingNameInfo><SettingID>1343646814</SettingID><SettingValue>0</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>CUDA - Sysmem Fallback Policy</SettingNameInfo><SettingID>283962569</SettingID><SettingValue>1</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Power Management - Mode</SettingNameInfo><SettingID>274197361</SettingID><SettingValue>1</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Shader Cache - Cache Size</SettingNameInfo><SettingID>11306135</SettingID><SettingValue>4294967295</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Threaded Optimization</SettingNameInfo><SettingID>549528094</SettingID><SettingValue>1</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>OpenGL GDI Compatibility</SettingNameInfo><SettingID>544392611</SettingID><SettingValue>0</SettingValue><ValueType>Dword</ValueType></ProfileSetting>
              <ProfileSetting><SettingNameInfo>Preferred OpenGL GPU</SettingNameInfo><SettingID>550564838</SettingID><SettingValue>id,2.0:268410DE,00000100,GF - (400,2,161,24564) @ (0)</SettingValue><ValueType>String</ValueType></ProfileSetting>
            </Settings>
          </Profile>
        </ArrayOfProfile>
        """;
}
