using AkariDash.Core.Gpus.NvApi;
using Xunit;

namespace AkariDash.Tests;

/// <summary>
/// The ids nvapi_QueryInterface takes, against the table in NVIDIA's nvapi_interface.h
/// (github.com/NVIDIA/nvapi, MIT). The expected values are copied from that file.
/// </summary>
public class NvApiIdsTests
{
    [Theory]
    [InlineData(NvApiIds.Initialize, 0x0150e828u)]
    [InlineData(NvApiIds.Unload, 0xd22bdd7eu)]
    [InlineData(NvApiIds.GetErrorMessage, 0x6c2d048cu)]
    [InlineData(NvApiIds.EnumPhysicalGPUs, 0xe5ac921fu)]
    [InlineData(NvApiIds.GPU_GetConnectedDisplayIds, 0x0078dba2u)]
    [InlineData(NvApiIds.EnumNvidiaDisplayHandle, 0x9abdd40du)]
    [InlineData(NvApiIds.GetAssociatedNvidiaDisplayName, 0x22a78b05u)]
    [InlineData(NvApiIds.DISP_GetDisplayIdByDisplayName, 0xae457190u)]
    [InlineData(NvApiIds.Disp_ColorControl, 0x92f9d80du)]
    [InlineData(NvApiIds.Disp_GetHdrCapabilities, 0x84f2a8dfu)]
    [InlineData(NvApiIds.Disp_HdrColorControl, 0x351da224u)]
    [InlineData(NvApiIds.DISP_GetDisplayConfig, 0x11abccf8u)]
    [InlineData(NvApiIds.DISP_SetDisplayConfig, 0x5d8cf8deu)]
    public void Ids_match_nvidias_interface_table(uint id, uint expected)
    {
        Assert.Equal(expected, id);
    }
}
