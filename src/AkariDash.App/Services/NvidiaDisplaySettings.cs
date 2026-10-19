using System.Runtime.InteropServices;
using AkariDash.Core.Gpus;
using AkariDash.Core.Gpus.NvApi;

namespace AkariDash.App.Services;

/// <summary>A display's colour output, in NVAPI's values: NV_COLOR_FORMAT, NV_DYNAMIC_RANGE and NV_BPC.</summary>
internal readonly record struct NvidiaColorOutput(byte Format, byte DynamicRange, int Bpc);

/// <summary>
/// The NVAPI half of the GPUs page's Settings tab, one display at a time, called the way Vain Toolbox calls
/// it (read out with REA): Disp_ColorControl for colour format, range and depth, Disp_HdrColorControl for
/// HDR10, and DISP_Get/SetDisplayConfig for the scaling of one display.
/// </summary>
internal static class NvidiaDisplaySettings
{
    private const byte ColorCmdGet = 1;
    private const byte ColorCmdSet = 2;
    private const byte ColorimetryRgb = 0;
    private const byte ColorimetryYcc709 = 2;
    private const int HdrCmdGet = 0;
    private const int HdrCmdSet = 1;
    private const int HdrModeOff = 0;
    private const int HdrModeUhda = 2;
    private const uint DisplayConfigValidateOnly = 1;
    private const uint DisplayConfigSaveToPersistence = 2;
    private const int TimingOverrideAuto = 1;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ColorControlV5(uint displayId, ref NvColorDataV5 data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ColorControlV3(uint displayId, ref NvColorDataV3 data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetHdrCapabilities(uint displayId, ref NvHdrCapabilitiesV3 capabilities);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int HdrColorControl(uint displayId, ref NvHdrColorDataV2 data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetDisplayConfig(ref uint count, IntPtr paths);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetDisplayConfig(uint count, IntPtr paths, uint flags);

    private static NvApi Api => NvApi.Instance ?? throw new NvApiException("Initialize", NvApiStatus.NoImplementation);

    /// <summary>The colour output the display runs now, asked with NV_COLOR_DATA V5, or V3 on older drivers.</summary>
    public static NvidiaColorOutput ReadColor(uint displayId)
    {
        var control = Api.Function<ColorControlV5>(NvApiIds.Disp_ColorControl, "Disp_ColorControl");
        var v5 = new NvColorDataV5 { StructVersion = NvColorDataV5.Version, Size = (ushort)Marshal.SizeOf<NvColorDataV5>(), Cmd = ColorCmdGet };
        var status = control(displayId, ref v5);
        if (status == NvApiStatus.Ok)
        {
            return new(v5.Data.ColorFormat, v5.Data.DynamicRange, v5.Data.Bpc);
        }

        if (status != NvApiStatus.IncompatibleStructVersion)
        {
            throw new NvApiException("Disp_ColorControl", status);
        }

        var v3 = new NvColorDataV3 { StructVersion = NvColorDataV3.Version, Size = (ushort)Marshal.SizeOf<NvColorDataV3>(), Cmd = ColorCmdGet };
        NvApi.Check("Disp_ColorControl", Api.Function<ColorControlV3>(NvApiIds.Disp_ColorControl, "Disp_ColorControl")(displayId, ref v3));
        return new(v3.Data.ColorFormat, v3.Data.DynamicRange, v3.Data.Bpc);
    }

    /// <summary>
    /// Sets the colour output as Vain does: the user's own choice (NV_COLOR_SELECTION_POLICY_USER), BT.709
    /// colorimetry for YCbCr, V5 first and V3 when the driver does not know V5.
    /// </summary>
    public static void WriteColor(uint displayId, NvidiaColorOutput output)
    {
        var colorimetry = output.Format == 0 ? ColorimetryRgb : ColorimetryYcc709;
        var v5 = new NvColorDataV5
        {
            StructVersion = NvColorDataV5.Version,
            Size = (ushort)Marshal.SizeOf<NvColorDataV5>(),
            Cmd = ColorCmdSet,
            Data = new() { ColorFormat = output.Format, Colorimetry = colorimetry, DynamicRange = output.DynamicRange, Bpc = output.Bpc },
        };
        var status = Api.Function<ColorControlV5>(NvApiIds.Disp_ColorControl, "Disp_ColorControl")(displayId, ref v5);
        if (status == NvApiStatus.Ok)
        {
            return;
        }

        var v3 = new NvColorDataV3
        {
            StructVersion = NvColorDataV3.Version,
            Size = (ushort)Marshal.SizeOf<NvColorDataV3>(),
            Cmd = ColorCmdSet,
            Data = new() { ColorFormat = output.Format, Colorimetry = colorimetry, DynamicRange = output.DynamicRange, Bpc = output.Bpc },
        };
        NvApi.Check("Disp_ColorControl", Api.Function<ColorControlV3>(NvApiIds.Disp_ColorControl, "Disp_ColorControl")(displayId, ref v3));
    }

    /// <summary>Whether the display takes HDR10 (ST 2084), as NVIDIA reads its EDID.</summary>
    public static bool SupportsHdr10(uint displayId)
    {
        var capabilities = new NvHdrCapabilitiesV3 { StructVersion = NvHdrCapabilitiesV3.Version };
        var status = Api.Function<GetHdrCapabilities>(NvApiIds.Disp_GetHdrCapabilities, "Disp_GetHdrCapabilities")(displayId, ref capabilities);
        return status == NvApiStatus.Ok && (capabilities.Flags & NvHdrCapabilitiesV3.St2084EotfSupported) != 0;
    }

    public static bool ReadHdr10(uint displayId)
    {
        var data = new NvHdrColorDataV2 { StructVersion = NvHdrColorDataV2.Version, Cmd = HdrCmdGet };
        NvApi.Check("Disp_HdrColorControl", Api.Function<HdrColorControl>(NvApiIds.Disp_HdrColorControl, "Disp_HdrColorControl")(displayId, ref data));
        return data.HdrMode != HdrModeOff;
    }

    /// <summary>HDR10 on (NV_HDR_MODE_UHDA) or off, which takes effect at once.</summary>
    public static void WriteHdr10(uint displayId, bool on)
    {
        var data = new NvHdrColorDataV2 { StructVersion = NvHdrColorDataV2.Version, Cmd = HdrCmdSet, HdrMode = on ? HdrModeUhda : HdrModeOff };
        NvApi.Check("Disp_HdrColorControl", Api.Function<HdrColorControl>(NvApiIds.Disp_HdrColorControl, "Disp_HdrColorControl")(displayId, ref data));
    }

    /// <summary>The display's NV_SCALING value, or null when it is not in the current display config.</summary>
    public static int? ReadScaling(uint displayId)
    {
        using var config = DisplayConfig.Read();
        return config.Details(displayId) is { } details ? Marshal.PtrToStructure<NvDisplayConfigPathAdvancedTargetInfoV1>(details).Scaling : null;
    }

    /// <summary>
    /// Sets the display's NV_SCALING value the way Vain's FUN_1401b3800 does: validate first, then save; a
    /// new timing when the scaling moves between GPU and display; the old config put back if saving fails.
    /// </summary>
    public static void WriteScaling(uint displayId, int scaling)
    {
        using var original = DisplayConfig.Read();
        using var config = DisplayConfig.Read();
        if (config.Details(displayId) is not { } details)
        {
            throw new InvalidOperationException("The display is not part of the desktop, so its scaling cannot be set.");
        }

        var advanced = Marshal.PtrToStructure<NvDisplayConfigPathAdvancedTargetInfoV1>(details);
        if (NvidiaScaling.NeedsNewTiming(advanced.Scaling, scaling))
        {
            advanced.TimingOverride = TimingOverrideAuto;
        }

        advanced.Scaling = scaling;
        Marshal.StructureToPtr(advanced, details, fDeleteOld: false);

        var set = Api.Function<SetDisplayConfig>(NvApiIds.DISP_SetDisplayConfig, "DISP_SetDisplayConfig");
        var status = set(config.Count, config.Paths, DisplayConfigValidateOnly);
        if (status == NvApiStatus.Ok)
        {
            status = set(config.Count, config.Paths, DisplayConfigSaveToPersistence);
        }

        if (status != NvApiStatus.Ok)
        {
            if (ReadScaling(displayId) != Marshal.PtrToStructure<NvDisplayConfigPathAdvancedTargetInfoV1>(original.Details(displayId)!.Value).Scaling)
            {
                set(original.Count, original.Paths, DisplayConfigSaveToPersistence);
            }

            throw new NvApiException("DISP_SetDisplayConfig", status);
        }
    }

    /// <summary>
    /// The current display config in unmanaged memory, read in NVIDIA's three passes: the number of paths,
    /// then each path's number of targets, then the targets with their details and the source modes.
    /// </summary>
    private sealed class DisplayConfig : IDisposable
    {
        private readonly List<IntPtr> _allocations = [];

        private DisplayConfig()
        {
        }

        public uint Count { get; private set; }

        public IntPtr Paths { get; private set; }

        public static DisplayConfig Read()
        {
            var get = Api.Function<GetDisplayConfig>(NvApiIds.DISP_GetDisplayConfig, "DISP_GetDisplayConfig");
            var config = new DisplayConfig();
            try
            {
                uint count = 0;
                NvApi.Check("DISP_GetDisplayConfig", get(ref count, IntPtr.Zero));

                var pathSize = Marshal.SizeOf<NvDisplayConfigPathInfoV2>();
                config.Paths = config.Allocate(pathSize * (int)count);
                for (var i = 0; i < count; i++)
                {
                    Marshal.StructureToPtr(new NvDisplayConfigPathInfoV2 { StructVersion = NvDisplayConfigPathInfoV2.Version }, config.Paths + i * pathSize, false);
                }

                NvApi.Check("DISP_GetDisplayConfig", get(ref count, config.Paths));

                var targetSize = Marshal.SizeOf<NvDisplayConfigPathTargetInfoV2>();
                var detailsSize = Marshal.SizeOf<NvDisplayConfigPathAdvancedTargetInfoV1>();
                for (var i = 0; i < count; i++)
                {
                    var path = Marshal.PtrToStructure<NvDisplayConfigPathInfoV2>(config.Paths + i * pathSize);
                    path.SourceModeInfo = config.Allocate(Marshal.SizeOf<NvDisplayConfigSourceModeInfoV1>());
                    path.TargetInfo = config.Allocate(targetSize * (int)path.TargetInfoCount);
                    for (var t = 0; t < path.TargetInfoCount; t++)
                    {
                        var details = config.Allocate(detailsSize);
                        Marshal.StructureToPtr(new NvDisplayConfigPathAdvancedTargetInfoV1 { StructVersion = NvDisplayConfigPathAdvancedTargetInfoV1.Version }, details, false);
                        Marshal.StructureToPtr(new NvDisplayConfigPathTargetInfoV2 { Details = details }, path.TargetInfo + t * targetSize, false);
                    }

                    Marshal.StructureToPtr(path, config.Paths + i * pathSize, false);
                }

                NvApi.Check("DISP_GetDisplayConfig", get(ref count, config.Paths));
                config.Count = count;
                return config;
            }
            catch
            {
                config.Dispose();
                throw;
            }
        }

        /// <summary>The advanced details of the target showing <paramref name="displayId"/>, if any path has it.</summary>
        public IntPtr? Details(uint displayId)
        {
            var pathSize = Marshal.SizeOf<NvDisplayConfigPathInfoV2>();
            var targetSize = Marshal.SizeOf<NvDisplayConfigPathTargetInfoV2>();
            for (var i = 0; i < Count; i++)
            {
                var path = Marshal.PtrToStructure<NvDisplayConfigPathInfoV2>(Paths + i * pathSize);
                for (var t = 0; t < path.TargetInfoCount; t++)
                {
                    var target = Marshal.PtrToStructure<NvDisplayConfigPathTargetInfoV2>(path.TargetInfo + t * targetSize);
                    if (target.DisplayId == displayId && target.Details != IntPtr.Zero)
                    {
                        return target.Details;
                    }
                }
            }

            return null;
        }

        public void Dispose()
        {
            foreach (var allocation in _allocations)
            {
                Marshal.FreeHGlobal(allocation);
            }

            _allocations.Clear();
        }

        private IntPtr Allocate(int size)
        {
            size = Math.Max(size, 1);
            var memory = Marshal.AllocHGlobal(size);
            _allocations.Add(memory);
            Marshal.Copy(new byte[size], 0, memory, size);
            return memory;
        }
    }
}
