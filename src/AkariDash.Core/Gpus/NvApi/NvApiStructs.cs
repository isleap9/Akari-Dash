using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AkariDash.Core.Gpus.NvApi;

// The structs NVAPI functions take, declared field by field as NVIDIA's nvapi.h (github.com/NVIDIA/nvapi,
// MIT) declares them, with sequential layout so the padding comes out as a C compiler lays it for x64.
// Parts nothing here reads are kept as opaque bytes of the header's size. NVAPI checks a struct by its
// version: its size in the low 16 bits and its revision above (MAKE_NVAPI_VERSION in the header).

internal static class NvApiVersion
{
    public static uint Of<T>(int revision) where T : struct => (uint)Marshal.SizeOf<T>() | ((uint)revision << 16);
}

/// <summary>NV_COLOR_DATA_V3: what drivers without V5 take for NvAPI_Disp_ColorControl.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct NvColorDataV3
{
    public static readonly uint Version = NvApiVersion.Of<NvColorDataV3>(3);

    public uint StructVersion;
    public ushort Size;
    public byte Cmd;
    public NvColorDataV3Data Data;
}

[StructLayout(LayoutKind.Sequential)]
public struct NvColorDataV3Data
{
    public byte ColorFormat;
    public byte Colorimetry;
    public byte DynamicRange;
    public int Bpc;
}

/// <summary>NV_COLOR_DATA_V5, for NvAPI_Disp_ColorControl: colour format, dynamic range and depth.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct NvColorDataV5
{
    public static readonly uint Version = NvApiVersion.Of<NvColorDataV5>(5);

    public uint StructVersion;
    public ushort Size;
    public byte Cmd;
    public NvColorDataV5Data Data;
}

/// <summary>The data part of NV_COLOR_DATA_V5. Its enums align it to 4, so it starts at offset 8.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct NvColorDataV5Data
{
    public byte ColorFormat;
    public byte Colorimetry;
    public byte DynamicRange;
    public int Bpc;
    public int ColorSelectionPolicy;
    public int Depth;
}

/// <summary>
/// NV_HDR_COLOR_DATA_V2, for NvAPI_Disp_HdrColorControl. Vain Toolbox passes 0x20030 here, built against an
/// older nvapi.h without hdrBpc; this is the current header's 52-byte layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NvHdrColorDataV2
{
    public static readonly uint Version = NvApiVersion.Of<NvHdrColorDataV2>(2);

    public uint StructVersion;
    public int Cmd;
    public int HdrMode;
    public int StaticMetadataDescriptorId;
    public MasteringDisplayData MasteringDisplayData;
    public int HdrColorFormat;
    public int HdrDynamicRange;
    public int HdrBpc;
}

/// <summary>The twelve 16-bit values of the mastering display's static metadata (CEA-861.3).</summary>
[InlineArray(12)]
public struct MasteringDisplayData
{
    private ushort _element;
}

/// <summary>NV_HDR_CAPABILITIES_V3, from NvAPI_Disp_GetHdrCapabilities. Only the support bits are read.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct NvHdrCapabilitiesV3
{
    public static readonly uint Version = NvApiVersion.Of<NvHdrCapabilitiesV3>(3);

    /// <summary>isST2084EotfSupported: the display takes HDR10.</summary>
    public const uint St2084EotfSupported = 1u << 0;

    public uint StructVersion;
    public uint Flags;
    public int StaticMetadataDescriptorId;
    public HdrCapabilitiesRest Rest;
}

/// <summary>display_data and dv_static_metadata: 52 bytes, aligned to 4.</summary>
[InlineArray(13)]
public struct HdrCapabilitiesRest
{
    private uint _element;
}

/// <summary>NV_GPU_DISPLAYIDS, from NvAPI_GPU_GetConnectedDisplayIds. Its VER2 is revision 3 in the header.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct NvGpuDisplayIds
{
    public static readonly uint Version = NvApiVersion.Of<NvGpuDisplayIds>(3);

    /// <summary>isActive: the display is part of the desktop.</summary>
    public const uint IsActive = 1u << 2;

    public uint StructVersion;
    public int ConnectorType;
    public uint DisplayId;
    public uint Flags;
}

/// <summary>NV_DISPLAYCONFIG_PATH_INFO_V2, for NvAPI_DISP_GetDisplayConfig and SetDisplayConfig.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct NvDisplayConfigPathInfoV2
{
    public static readonly uint Version = NvApiVersion.Of<NvDisplayConfigPathInfoV2>(2);

    public uint StructVersion;
    public uint SourceId;
    public uint TargetInfoCount;
    public IntPtr TargetInfo;
    public IntPtr SourceModeInfo;
    public uint Flags;
    public IntPtr OsAdapterId;
}

/// <summary>NV_DISPLAYCONFIG_PATH_TARGET_INFO_V2: one display of a path, with its advanced details.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct NvDisplayConfigPathTargetInfoV2
{
    public uint DisplayId;
    public IntPtr Details;
    public uint TargetId;
}

/// <summary>NV_DISPLAYCONFIG_PATH_ADVANCED_TARGET_INFO_V1: rotation, scaling and timing of one display.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct NvDisplayConfigPathAdvancedTargetInfoV1
{
    public static readonly uint Version = NvApiVersion.Of<NvDisplayConfigPathAdvancedTargetInfoV1>(1);

    public uint StructVersion;
    public int Rotation;
    public int Scaling;
    public uint RefreshRate1K;
    public uint Flags;
    public int Connector;
    public int TvFormat;
    public int TimingOverride;
    public NvTiming Timing;
}

/// <summary>NV_TIMING: 96 bytes, aligned to 4. Passed back to the driver as it gave it.</summary>
[InlineArray(24)]
public struct NvTiming
{
    private uint _element;
}

/// <summary>NV_DISPLAYCONFIG_SOURCE_MODE_INFO_V1: resolution, position and flags of a path's source.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct NvDisplayConfigSourceModeInfoV1
{
    public uint Width;
    public uint Height;
    public uint ColorDepth;
    public int ColorFormat;
    public int X;
    public int Y;
    public int SpanningOrientation;
    public uint Flags;
}

/// <summary>NV_EDID_V3, from NvAPI_GPU_GetEDID: up to 256 bytes of the monitor's EDID at a time.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct NvEdidV3
{
    public static readonly uint Version = NvApiVersion.Of<NvEdidV3>(3);

    public uint StructVersion;
    public EdidData Data;
    public uint SizeOfEdid;
    public uint EdidId;
    public uint Offset;
}

/// <summary>EDID_Data: 256 bytes.</summary>
[InlineArray(256)]
public struct EdidData
{
    private byte _element;
}
