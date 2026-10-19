using System.Runtime.InteropServices;
using System.Text;
using AkariDash.Core.Gpus;
using AkariDash.Core.Gpus.NvApi;
using Microsoft.Win32;

namespace AkariDash.App.Services;

/// <summary>
/// One display of an NVIDIA card as the Settings tab's Display Adapter box lists it: NVAPI's display id,
/// the monitor's name from its EDID (or "Display N"), its key under DisplayDatabase, and its display
/// handle for the vibrance and hue calls.
/// </summary>
public sealed record NvidiaDisplay(uint DisplayId, string Name, string? DisplayDatabaseKey, IntPtr Handle);

/// <summary>
/// The displays of an NVIDIA card, found the way Vain Toolbox finds them (REA, FUN_14030d6f0): the card's
/// NVAPI GPU by PCI bus, GPU_GetConnectedDisplayIds keeping the active, OS-visible or connected ones, the
/// name and DisplayDatabase key from GPU_GetEDID. Vain pairs display handles with displays by position;
/// here each handle is matched by its name through DISP_GetDisplayIdByDisplayName instead.
/// </summary>
internal static class NvidiaDisplays
{
    public const string DisplayDatabaseKey = @"SYSTEM\CurrentControlSet\Services\nvlddmkm\State\DisplayDatabase";

    private const int EndEnumeration = -7;
    private const uint Active = 1u << 2;
    private const uint OsVisible = 1u << 4;
    private const uint Connected = 1u << 6;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EnumPhysicalGpus([Out] IntPtr[] handles, out uint count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetBusId(IntPtr gpu, out uint busId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetConnectedDisplayIds(IntPtr gpu, [In, Out] NvGpuDisplayIds[]? ids, ref uint count, uint flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetGpuAndOutputIdFromDisplayId(uint displayId, out IntPtr gpu, out uint outputId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetEdid(IntPtr gpu, uint outputId, ref NvEdidV3 edid);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EnumNvidiaDisplayHandle(uint index, out IntPtr handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetAssociatedNvidiaDisplayName(IntPtr handle, [Out] byte[] name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetDisplayIdByDisplayName([MarshalAs(UnmanagedType.LPStr)] string name, out uint displayId);

    /// <summary>The card's displays; empty for other makers' cards, without NVAPI, or on any failure.</summary>
    public static IReadOnlyList<NvidiaDisplay> Read(GpuCard card)
    {
        if (NvApi.Instance is not { } api || Gpu(api, card) is not { } gpu)
        {
            return [];
        }

        try
        {
            var handles = Handles(api);
            var displays = new List<NvidiaDisplay>();
            foreach (var id in ConnectedIds(api, gpu))
            {
                var edid = ReadEdid(api, id);
                var candidate = edid is null ? null : Edid.DisplayDatabaseKey(edid);
                displays.Add(new(
                    id,
                    (edid is null ? null : Edid.MonitorName(edid)) ?? $"Display {displays.Count + 1}",
                    candidate is null ? null : Edid.FindDisplayDatabaseKey(candidate, DisplayDatabaseKeys(), HasValidScalingConfig),
                    handles.GetValueOrDefault(id)));
            }

            return displays;
        }
        catch (NvApiException)
        {
            return [];
        }
    }

    /// <summary>The monitor's ScalingConfig under DisplayDatabase, or null when it has none.</summary>
    public static byte[]? ReadScalingConfig(string key)
    {
        using var monitor = Registry.LocalMachine.OpenSubKey($@"{DisplayDatabaseKey}\{key}", writable: false);
        return monitor?.GetValue("ScalingConfig") as byte[];
    }

    private static bool HasValidScalingConfig(string key) => ReadScalingConfig(key) is { } blob && ScalingConfig.IsValid(blob);

    private static IEnumerable<string> DisplayDatabaseKeys()
    {
        using var database = Registry.LocalMachine.OpenSubKey(DisplayDatabaseKey, writable: false);
        return database?.GetSubKeyNames() ?? [];
    }

    // The card's NVAPI GPU: the one on its PCI bus, or the only one there is.
    private static IntPtr? Gpu(NvApi api, GpuCard card)
    {
        if (card.Vendor != Core.Machine.GpuVendor.Nvidia)
        {
            return null;
        }

        var handles = new IntPtr[64];
        if (api.Function<EnumPhysicalGpus>(NvApiIds.EnumPhysicalGPUs, "EnumPhysicalGPUs")(handles, out var count) != NvApiStatus.Ok || count == 0)
        {
            return null;
        }

        if (count == 1)
        {
            return handles[0];
        }

        var busId = api.Function<GetBusId>(NvApiIds.GPU_GetBusId, "GPU_GetBusId");
        foreach (var gpu in handles.Take((int)count))
        {
            if (busId(gpu, out var bus) == NvApiStatus.Ok && bus == card.BusNumber)
            {
                return gpu;
            }
        }

        return null;
    }

    private static IEnumerable<uint> ConnectedIds(NvApi api, IntPtr gpu)
    {
        var get = api.Function<GetConnectedDisplayIds>(NvApiIds.GPU_GetConnectedDisplayIds, "GPU_GetConnectedDisplayIds");
        uint count = 0;
        NvApi.Check("GPU_GetConnectedDisplayIds", get(gpu, null, ref count, 0));
        if (count == 0)
        {
            return [];
        }

        var ids = Enumerable.Repeat(new NvGpuDisplayIds { StructVersion = NvGpuDisplayIds.Version }, (int)count).ToArray();
        NvApi.Check("GPU_GetConnectedDisplayIds", get(gpu, ids, ref count, 0));
        return ids.Take((int)count)
            .Where(id => (id.Flags & (Active | OsVisible | Connected)) != 0)
            .Select(id => id.DisplayId)
            .ToList();
    }

    // The first block of the monitor's EDID, as Vain reads it with NV_EDID V3.
    private static byte[]? ReadEdid(NvApi api, uint displayId)
    {
        if (api.Function<GetGpuAndOutputIdFromDisplayId>(NvApiIds.SYS_GetGpuAndOutputIdFromDisplayId, "SYS_GetGpuAndOutputIdFromDisplayId")(displayId, out var gpu, out var outputId) != NvApiStatus.Ok)
        {
            return null;
        }

        var edid = new NvEdidV3 { StructVersion = NvEdidV3.Version };
        if (api.Function<GetEdid>(NvApiIds.GPU_GetEDID, "GPU_GetEDID")(gpu, outputId, ref edid) != NvApiStatus.Ok || edid.SizeOfEdid < 128)
        {
            return null;
        }

        ReadOnlySpan<byte> data = edid.Data;
        return data[..(int)Math.Min(edid.SizeOfEdid, 256)].ToArray();
    }

    // Every NVIDIA display handle, keyed by the display id its \\.\DISPLAYn name resolves to.
    private static Dictionary<uint, IntPtr> Handles(NvApi api)
    {
        var handles = new Dictionary<uint, IntPtr>();
        var enumerate = api.Function<EnumNvidiaDisplayHandle>(NvApiIds.EnumNvidiaDisplayHandle, "EnumNvidiaDisplayHandle");
        var nameOf = api.Function<GetAssociatedNvidiaDisplayName>(NvApiIds.GetAssociatedNvidiaDisplayName, "GetAssociatedNvidiaDisplayName");
        var idOf = api.Function<GetDisplayIdByDisplayName>(NvApiIds.DISP_GetDisplayIdByDisplayName, "DISP_GetDisplayIdByDisplayName");
        for (uint i = 0; i < 64; i++)
        {
            var status = enumerate(i, out var handle);
            if (status == EndEnumeration || status != NvApiStatus.Ok)
            {
                break;
            }

            var name = new byte[64];
            if (nameOf(handle, name) == NvApiStatus.Ok &&
                idOf(Encoding.ASCII.GetString(name).TrimEnd('\0'), out var displayId) == NvApiStatus.Ok)
            {
                handles[displayId] = handle;
            }
        }

        return handles;
    }
}
