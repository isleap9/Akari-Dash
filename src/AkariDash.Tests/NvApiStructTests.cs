using System.Runtime.InteropServices;
using AkariDash.Core.Gpus.NvApi;
using Xunit;

namespace AkariDash.Tests;

/// <summary>
/// Every struct handed to the driver has the size, version and field offsets NVIDIA's own header gives
/// it. The expected numbers were printed by a C program compiled with gcc against nvapi.h from
/// github.com/NVIDIA/nvapi (sizeof, offsetof and MAKE_NVAPI_VERSION, x64), not worked out from the C#
/// declarations.
/// </summary>
public class NvApiStructTests
{
    [Theory]
    [InlineData(typeof(NvColorDataV3), 16, 0x00030010u)]
    [InlineData(typeof(NvColorDataV5), 24, 0x00050018u)]
    [InlineData(typeof(NvHdrColorDataV2), 52, 0x00020034u)]
    [InlineData(typeof(NvHdrCapabilitiesV3), 64, 0x00030040u)]
    [InlineData(typeof(NvGpuDisplayIds), 16, 0x00030010u)]
    [InlineData(typeof(NvDisplayConfigPathInfoV2), 48, 0x00020030u)]
    [InlineData(typeof(NvDisplayConfigPathAdvancedTargetInfoV1), 128, 0x00010080u)]
    [InlineData(typeof(NvEdidV3), 272, 0x00030110u)]
    public void Versioned_structs_match_the_header(Type type, int size, uint version)
    {
        Assert.Equal(size, Marshal.SizeOf(type));
        Assert.Equal(version, (uint)type.GetField("Version")!.GetValue(null)!);
    }

    [Theory]
    [InlineData(typeof(NvDisplayConfigPathTargetInfoV2), 24)]
    [InlineData(typeof(NvDisplayConfigSourceModeInfoV1), 32)]
    public void Unversioned_structs_match_the_header(Type type, int size)
    {
        Assert.Equal(size, Marshal.SizeOf(type));
    }

    [Theory]
    [InlineData(typeof(NvColorDataV5), "Data", 8)]
    [InlineData(typeof(NvHdrColorDataV2), "HdrMode", 8)]
    [InlineData(typeof(NvGpuDisplayIds), "DisplayId", 8)]
    [InlineData(typeof(NvDisplayConfigPathInfoV2), "TargetInfoCount", 8)]
    [InlineData(typeof(NvDisplayConfigPathInfoV2), "TargetInfo", 16)]
    [InlineData(typeof(NvDisplayConfigPathInfoV2), "SourceModeInfo", 24)]
    [InlineData(typeof(NvDisplayConfigPathInfoV2), "OsAdapterId", 40)]
    [InlineData(typeof(NvDisplayConfigPathTargetInfoV2), "Details", 8)]
    [InlineData(typeof(NvDisplayConfigPathTargetInfoV2), "TargetId", 16)]
    [InlineData(typeof(NvDisplayConfigPathAdvancedTargetInfoV1), "Scaling", 8)]
    [InlineData(typeof(NvDisplayConfigPathAdvancedTargetInfoV1), "Connector", 20)]
    [InlineData(typeof(NvDisplayConfigPathAdvancedTargetInfoV1), "TimingOverride", 28)]
    [InlineData(typeof(NvDisplayConfigPathAdvancedTargetInfoV1), "Timing", 32)]
    [InlineData(typeof(NvEdidV3), "SizeOfEdid", 260)]
    [InlineData(typeof(NvEdidV3), "EdidId", 264)]
    [InlineData(typeof(NvEdidV3), "Offset", 268)]
    public void Fields_sit_where_the_header_puts_them(Type type, string field, int offset)
    {
        Assert.Equal(offset, (int)Marshal.OffsetOf(type, field));
    }

    [Theory]
    [InlineData("Bpc", 4)]
    [InlineData("ColorSelectionPolicy", 8)]
    [InlineData("Depth", 12)]
    public void Color_data_v5_fields_sit_where_the_header_puts_them(string field, int offsetInData)
    {
        // nvapi.h: data.bpc @12, data.colorSelectionPolicy @16, data.depth @20; data itself @8.
        Assert.Equal(offsetInData, (int)Marshal.OffsetOf<NvColorDataV5Data>(field));
    }
}
