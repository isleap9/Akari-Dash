using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AkariDash.Core.Gpus.NvApi;

/// <summary>
/// NVAPI functions NVIDIA's headers do not publish, used by the Settings tab for Digital Vibrance, Hue and
/// Brightness/Contrast/Gamma. The one exception to taking NVAPI from NVIDIA's MIT headers (ADR-0003,
/// amended 2026-10-19 at the user's call): the ids, struct versions and field use are what Vain Toolbox
/// passes, read out of its binary with REA. Names follow the ones the community uses for these ids.
/// </summary>
public static class NvApiUndocumented
{
    public const uint GetDVCInfoEx = 0x0e45002d;
    public const uint SetDVCLevelEx = 0x4a82c2b1;
    public const uint GetHUEInfo = 0x95b64341;
    public const uint SetHUEAngle = 0xf5a0f22c;

    /// <summary>The per-channel colour table Vain writes Brightness/Contrast/Gamma through (its FUN_1401b3600).</summary>
    public const uint SetColorTable = 0x7082a053;
}

/// <summary>
/// Digital Vibrance, for GetDVCInfoEx and SetDVCLevelEx (display handle, output 0). Vain stores all four
/// values; its slider runs from 0 to <see cref="MaxLevel"/>, and a set passes the new level in
/// <see cref="CurrentLevel"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NvDisplayDvcInfoEx
{
    public static readonly uint Version = NvApiVersion.Of<NvDisplayDvcInfoEx>(1);

    public uint StructVersion;
    public int CurrentLevel;
    public int MinLevel;
    public int MaxLevel;
    public int DefaultLevel;
}

/// <summary>Hue, from GetHUEInfo (display handle, output 0); SetHUEAngle takes the angle itself.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct NvDisplayHueInfo
{
    public static readonly uint Version = NvApiVersion.Of<NvDisplayHueInfo>(1);

    public uint StructVersion;
    public int CurrentAngle;
    public int DefaultAngle;
}

/// <summary>
/// The colour table for <see cref="NvApiUndocumented.SetColorTable"/> (display id): 1024 steps of red,
/// green and blue floats (<see cref="ColorRamp"/>), then a DWORD Vain always sets to 1.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NvColorTable
{
    public static readonly uint Version = NvApiVersion.Of<NvColorTable>(1);

    public uint StructVersion;
    public ColorTableSteps Steps;
    public int Enabled;
}

/// <summary>3072 floats: red, green and blue for each of 1024 steps.</summary>
[InlineArray(ColorRamp.Steps * 3)]
public struct ColorTableSteps
{
    private float _element;
}
