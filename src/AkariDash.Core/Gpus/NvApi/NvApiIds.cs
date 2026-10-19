namespace AkariDash.Core.Gpus.NvApi;

/// <summary>
/// The ids nvapi_QueryInterface resolves to driver functions, from the table in NVIDIA's
/// nvapi_interface.h (github.com/NVIDIA/nvapi, MIT). Named as NVIDIA names them, without "NvAPI_".
/// </summary>
public static class NvApiIds
{
    public const uint Initialize = 0x0150e828;
    public const uint Unload = 0xd22bdd7e;
    public const uint GetErrorMessage = 0x6c2d048c;
    public const uint EnumPhysicalGPUs = 0xe5ac921f;
    public const uint GPU_GetConnectedDisplayIds = 0x0078dba2;
    public const uint GPU_GetBusId = 0x1be0b8e5;
    public const uint GPU_GetEDID = 0x37d32e69;
    public const uint SYS_GetGpuAndOutputIdFromDisplayId = 0x112ba1a5;
    public const uint EnumNvidiaDisplayHandle = 0x9abdd40d;
    public const uint GetAssociatedNvidiaDisplayName = 0x22a78b05;
    public const uint DISP_GetDisplayIdByDisplayName = 0xae457190;
    public const uint Disp_ColorControl = 0x92f9d80d;
    public const uint Disp_GetHdrCapabilities = 0x84f2a8df;
    public const uint Disp_HdrColorControl = 0x351da224;
    public const uint DISP_GetDisplayConfig = 0x11abccf8;
    public const uint DISP_SetDisplayConfig = 0x5d8cf8de;
}
