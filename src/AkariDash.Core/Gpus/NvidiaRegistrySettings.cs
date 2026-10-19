using System.Globalization;
using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Gpus;

/// <summary>
/// One of the NVIDIA driver's registry values on the GPUs page, as Vain Toolbox lists it: a DWORD the
/// driver (nvlddmkm) reads at start, the value Vain recommends, and its group. Most live under the
/// card's own display-class key; one with <paramref name="ServiceKey"/> lives under that service.
/// </summary>
public sealed record NvidiaRegistrySetting(string Name, uint Recommended, string Group, string? ServiceKey = null)
{
    private const string DisplayClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\";

    /// <summary>Where the value lives for the card whose display-class key is <paramref name="cardKey"/> (e.g. <c>0000</c>).</summary>
    public RegistryLocation Location(string cardKey) => ServiceKey is null
        ? new RegistryLocation(RegistryHive.LocalMachine, DisplayClass + cardKey, Name)
        : new RegistryLocation(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\" + ServiceKey, Name);
}

/// <summary>
/// Vain Toolbox's NVIDIA registry settings (Main tab > Registry Settings), read from its binary
/// (FUN_140160c50) at the user's call (ADR-0003, amended 2026-10-19). The driver reads them when it
/// starts, so a change takes effect after Restart GPU or a reboot. Deleting a value gives the driver
/// back its own default.
/// </summary>
public static class NvidiaRegistrySettings
{
    private const string Power = "Power Management";

    public static IReadOnlyList<NvidiaRegistrySetting> All { get; } =
    [
        new("RmPowerFeature", 0x54455565, Power),
        new("RmPowerFeature2", 0x05555555, Power),
        new("D3PCLatency", 1, Power),
        new("EnablePerformanceMode", 1, Power),
        new("EnableRuntimePowerManagement", 0, Power),
        new("DisableDynamicPstate", 1, Power),
        new("EnableMsHybrid", 0, Power),
        new("RMElcg", 0x55555554, Power),
        new("RMGC6Feature", 0x000AAAAA, Power),
        new("RMGC6Parameters", 0x55, Power),
        new("RMGCOffFeature", 1, Power),
        new("RMDidleFeatureGC5", 0x02AA8AAA, Power),
        new("RMD3Feature", 2, Power),
        new("RMClkSlowDown", 0x04400000, Power),
        new("RmOptp2LowerMclk", 0, Power),
        new("RmOverrideIdleSlowdownSettings", 0, Power),
        new("RmClkPowerOffDramPllWhenUnused", 0, Power),
        new("RMLpwrArch", 0x00055555, Power),
        new("RMLpwrEiClient", 5, Power),
        new("RmLpwrCtrlGrRgParameters", 0x05555555, Power),
        new("RmLpwrCtrlMsDifrCgParameters", 0x555, Power),
        new("RmLpwrCtrlMsDifrSwAsrParameters", 0x1555, Power),
        new("RmLpwrCtrlMsLtcParameters", 5, Power),
        new("RmLpwrGrPgSwFilterFunction", 0, Power),
        new("RmLpwrCacheStatsOnD3", 0, Power),
        new("RmDwbMscg", 1, Power),
        new("SlideMCLK", 0, Power),
        new("RMEnableASPMAtLoad", 0, Power),
        new("RMDisableGpuASPMFlags", 3, Power),
        new("RMDisableOptimalPowerForPadlinkPll", 1, Power),
        new("RMForceRtd3D3Hot", 2, Power),
        new("RmMIONoPowerOff", 1, Power),
        new("EnableHDAudioD3Cold", 0, Power, ServiceKey: "nvlddmkm"),
        new("RM2644249", 1, Power),
        new("RM303107", 0x10, Power),
        new("RM2779240", 5, Power),
        new("RM580312", 1, Power),
        new("RMBug2519005War", 1, Power),
        new("RmCeElcgWar1895530", 1, Power),
        new("RmWar1760398", 1, Power),
        new("RMOPSB", 0x2AA2, Power),
        new("RmDisableInforomBBX", 0xF, Power),
        new("RmPgCtrlParameters", 0x55555155, Power),
        new("RmPgCtrlGrParameters", 0x55555555, Power),
        new("RMElpgStateOnInit", 3, Power),
    ];

    /// <summary>A typed value, read as up to eight hex digits with or without <c>0x</c>; null when it is not one.</summary>
    public static uint? ParseHex(string? typed)
    {
        var text = typed?.Trim() ?? string.Empty;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        return text.Length is >= 1 and <= 8 &&
            uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>A value as eight hex digits, the way Vain Toolbox shows it.</summary>
    public static string FormatHex(uint value) => value.ToString("X8", CultureInfo.InvariantCulture);
}
