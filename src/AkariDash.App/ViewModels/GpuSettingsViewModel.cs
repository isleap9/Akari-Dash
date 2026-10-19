using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using AkariDash.App.Services;
using AkariDash.Core.Gpus;
using AkariDash.Core.Machine;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

/// <summary>
/// The GPUs page's Settings tab, after Vain Toolbox's (read out with REA): one NVIDIA display at a time,
/// colour output and scaling on the left, HDR10 and colour adjustments on the right. Changes wait for
/// Apply, as in Vain, except HDR10, which Vain switches at once. Reset puts Vain's defaults back and
/// applies them; Revert puts back what was read when the display was picked.
/// </summary>
public sealed partial class GpuSettingsViewModel : ViewModelBase
{
    private const string PerformanceCountersKey = @"SYSTEM\CurrentControlSet\Services\nvlddmkm\Parameters\Global\NVTweak";
    private const string PerformanceCountersValue = "RmProfilingAdminOnly";

    // Vain's lists: Color Format RGB, YCbCr 4:4:4, 4:2:2, 4:2:0 (NV_COLOR_FORMAT 0, 2, 1, 3); Dynamic Range
    // Full and Limited (NV_DYNAMIC_RANGE 0, 1); Color Depth 8, 10 and 12 bpc (NV_BPC 2, 3, 4).
    private static readonly byte[] FormatValues = [0, 2, 1, 3];
    private static readonly byte[] RangeValues = [0, 1];
    private static readonly int[] DepthValues = [2, 3, 4];

    private readonly IMachine _machine;
    private readonly IDialogService _dialogs;
    private readonly IInfoBarService _infoBar;
    private readonly IStatusService _status;
    private readonly ILogger _logger;
    private readonly ChannelAdjustment[] _channels = [ColorRamp.Neutral, ColorRamp.Neutral, ColorRamp.Neutral];
    private GpuCard? _card;
    private Snapshot? _loaded;
    private (int Vibrance, int Hue) _defaults;
    private bool _loading;

    public GpuSettingsViewModel(IMachine machine, IDialogService dialogs, IInfoBarService infoBar, IStatusService status, ILogger logger)
    {
        _machine = machine;
        _dialogs = dialogs;
        _infoBar = infoBar;
        _status = status;
        _logger = logger;
    }

    public IReadOnlyList<string> ColorFormats { get; } = ["RGB", "YCbCr 4:4:4", "YCbCr 4:2:2", "YCbCr 4:2:0"];

    public IReadOnlyList<string> DynamicRanges { get; } = ["Full", "Limited"];

    public IReadOnlyList<string> ColorDepths { get; } = ["8 bpc", "10 bpc", "12 bpc"];

    public IReadOnlyList<string> ScalingModes { get; } = NvidiaScaling.ModeNames;

    public IReadOnlyList<string> ScalingPerformers { get; } = NvidiaScaling.PerformerNames;

    public IReadOnlyList<string> ColorChannels { get; } = ["All channels", "Red", "Green", "Blue"];

    [ObservableProperty]
    public partial IReadOnlyList<NvidiaDisplay> Displays { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasDisplays { get; private set; }

    [ObservableProperty]
    public partial bool HasNoDisplays { get; private set; } = true;

    [ObservableProperty]
    public partial NvidiaDisplay? SelectedDisplay { get; set; }

    [ObservableProperty]
    public partial int ColorFormatIndex { get; set; } = -1;

    [ObservableProperty]
    public partial int DynamicRangeIndex { get; set; } = -1;

    [ObservableProperty]
    public partial int ColorDepthIndex { get; set; } = -1;

    [ObservableProperty]
    public partial int ScalingModeIndex { get; set; } = -1;

    [ObservableProperty]
    public partial int ScalingPerformerIndex { get; set; } = -1;

    [ObservableProperty]
    public partial bool OverrideScaling { get; set; }

    /// <summary>The driver has written this monitor's ScalingConfig, which Vain only ever edits.</summary>
    [ObservableProperty]
    public partial bool OverrideScalingAvailable { get; private set; }

    [ObservableProperty]
    public partial bool PerformanceCounters { get; set; }

    [ObservableProperty]
    public partial bool Hdr10 { get; set; }

    [ObservableProperty]
    public partial bool Hdr10Supported { get; private set; }

    [ObservableProperty]
    public partial string Hdr10Support { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial int ColorChannelIndex { get; set; }

    [ObservableProperty]
    public partial double Brightness { get; set; } = 50;

    [ObservableProperty]
    public partial double Contrast { get; set; } = 50;

    [ObservableProperty]
    public partial double Gamma { get; set; } = 1.0;

    [ObservableProperty]
    public partial double Vibrance { get; set; }

    [ObservableProperty]
    public partial double VibranceMax { get; private set; } = 100;

    [ObservableProperty]
    public partial bool VibranceSupported { get; private set; }

    [ObservableProperty]
    public partial double Hue { get; set; }

    [ObservableProperty]
    public partial bool HueSupported { get; private set; }

    [ObservableProperty]
    public partial bool ReferenceMode { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(ResetCommand), nameof(RevertCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Lists the displays of <paramref name="card"/>, an NVIDIA card's, and reads the first.</summary>
    public void Load(GpuCard? card)
    {
        _card = card;
        Displays = card is null ? [] : NvidiaDisplays.Read(card);
        HasDisplays = Displays.Count > 0;
        HasNoDisplays = !HasDisplays;
        SelectedDisplay = Displays.FirstOrDefault();
    }

    partial void OnSelectedDisplayChanged(NvidiaDisplay? value)
    {
        if (value is not null)
        {
            Read(value);
        }
    }

    partial void OnColorChannelIndexChanged(int value)
    {
        var channel = _channels[Math.Max(0, value - 1)];
        var loading = _loading;
        _loading = true;
        Brightness = channel.Brightness;
        Contrast = channel.Contrast;
        Gamma = channel.Gamma;
        _loading = loading;
    }

    partial void OnBrightnessChanged(double value) => SetChannels(channel => channel with { Brightness = value });

    partial void OnContrastChanged(double value) => SetChannels(channel => channel with { Contrast = value });

    partial void OnGammaChanged(double value) => SetChannels(channel => channel with { Gamma = value });

    // "All channels" moves the three together; a single channel moves alone.
    private void SetChannels(Func<ChannelAdjustment, ChannelAdjustment> change)
    {
        if (_loading)
        {
            return;
        }

        for (var i = 0; i < 3; i++)
        {
            if (ColorChannelIndex == 0 || ColorChannelIndex == i + 1)
            {
                _channels[i] = change(_channels[i]);
            }
        }
    }

    // Vain switches HDR10 the moment its toggle moves (FUN_1401c7890).
    partial void OnHdr10Changed(bool value)
    {
        if (_loading || SelectedDisplay is not { } display)
        {
            return;
        }

        if (BuildInfo.IsDryRunOnly)
        {
            _infoBar.ShowInfo("HDR10", $"Dry Run: HDR10 was not turned {(value ? "on" : "off")}.");
            return;
        }

        try
        {
            NvidiaDisplaySettings.WriteHdr10(display.DisplayId, value);
            _loaded = _loaded is { } loaded ? loaded with { Hdr10 = value } : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Switching HDR10 failed");
            _ = _dialogs.ShowErrorAsync("HDR10", ex.Message);
            _loading = true;
            Hdr10 = !value;
            _loading = false;
        }
    }

    private void Read(NvidiaDisplay display)
    {
        _loading = true;
        try
        {
            var color = Try(() => NvidiaDisplaySettings.ReadColor(display.DisplayId), default(NvidiaColorOutput?));
            ColorFormatIndex = color is { } c ? Array.IndexOf(FormatValues, c.Format) : -1;
            DynamicRangeIndex = color is { } r ? Array.IndexOf(RangeValues, r.DynamicRange) : -1;
            ColorDepthIndex = color is { } d ? Array.IndexOf(DepthValues, d.Bpc) : -1;

            var scaling = Try<int?>(() => NvidiaDisplaySettings.ReadScaling(display.DisplayId), null);
            var pair = scaling is { } s ? NvidiaScaling.FromNvScaling(s) : null;
            ScalingModeIndex = pair is { } p ? (int)p.Mode : -1;
            ScalingPerformerIndex = pair is { } q ? (int)q.Performer : -1;

            var blob = display.DisplayDatabaseKey is { } key ? NvidiaDisplays.ReadScalingConfig(key) : null;
            OverrideScalingAvailable = blob is not null && ScalingConfig.IsValid(blob);
            OverrideScaling = OverrideScalingAvailable && scaling is { } current && ScalingConfig.IsOverriding(blob!, current);

            PerformanceCounters = _machine.Read(new RegistryLocation(RegistryHive.LocalMachine, PerformanceCountersKey, PerformanceCountersValue))
                is RegistryValue { Kind: RegistryValueKind.DWord, Data: 0 };

            Hdr10Supported = Try(() => NvidiaDisplaySettings.SupportsHdr10(display.DisplayId), false);
            Hdr10Support = Hdr10Supported ? "Monitor supports HDR10" : "Monitor does not support HDR10";
            Hdr10 = Hdr10Supported && Try(() => NvidiaDisplaySettings.ReadHdr10(display.DisplayId), false);

            // The driver does not report the colour table back, so the sliders start where Vain's do.
            Array.Fill(_channels, ColorRamp.Neutral);
            ColorChannelIndex = 0;
            Brightness = ColorRamp.Neutral.Brightness;
            Contrast = ColorRamp.Neutral.Contrast;
            Gamma = ColorRamp.Neutral.Gamma;

            var vibrance = Try(() => NvidiaDisplaySettings.ReadVibrance(display.Handle), default((int Level, int Max, int Default)?));
            VibranceSupported = vibrance is not null;
            VibranceMax = vibrance?.Max ?? 100;
            Vibrance = vibrance?.Level ?? 0;

            var hue = Try(() => NvidiaDisplaySettings.ReadHue(display.Handle), default((int Angle, int Default)?));
            HueSupported = hue is not null;
            Hue = hue?.Angle ?? 0;
            _defaults = (vibrance?.Default ?? 0, hue?.Default ?? 0);

            ReferenceMode = ReadReferenceMode(display.DisplayId);
            _loaded = Current();
        }
        finally
        {
            _loading = false;
        }
    }

    private bool CanChange() => SelectedDisplay is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanChange))]
    private void Revert()
    {
        if (_loaded is not { } loaded)
        {
            return;
        }

        _loading = true;
        ColorFormatIndex = loaded.ColorFormatIndex;
        DynamicRangeIndex = loaded.DynamicRangeIndex;
        ColorDepthIndex = loaded.ColorDepthIndex;
        ScalingModeIndex = loaded.ScalingModeIndex;
        ScalingPerformerIndex = loaded.ScalingPerformerIndex;
        OverrideScaling = loaded.OverrideScaling;
        PerformanceCounters = loaded.PerformanceCounters;
        loaded.Channels.CopyTo(_channels, 0);
        Vibrance = loaded.Vibrance;
        Hue = loaded.Hue;
        ReferenceMode = loaded.ReferenceMode;
        _loading = false;
        OnColorChannelIndexChanged(ColorChannelIndex);
    }

    /// <summary>
    /// Vain's Reset (FUN_1401cd560): 50, 50 and 1.0 on every channel, RGB, Full range and 8 bpc, the
    /// driver's own vibrance and hue, aspect-ratio scaling on the display when it takes it (else on the GPU),
    /// every switch off, then Apply.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task ResetAsync()
    {
        if (SelectedDisplay is not { } display ||
            !await _dialogs.ConfirmAsync(
                "Reset display settings?",
                $"Colour, scaling and the colour adjustments of {display.Name} go back to the defaults and are applied now.",
                "Reset",
                "Cancel"))
        {
            return;
        }

        _loading = true;
        ColorFormatIndex = 0;
        DynamicRangeIndex = 0;
        ColorDepthIndex = 0;
        var onDisplay = !BuildInfo.IsDryRunOnly && Try(() => NvidiaDisplaySettings.ScalingSupported(display.DisplayId, 6), false);
        ScalingModeIndex = (int)ScalingMode.AspectRatio;
        ScalingPerformerIndex = (int)(onDisplay ? ScalingPerformer.Display : ScalingPerformer.Gpu);
        OverrideScaling = false;
        PerformanceCounters = false;
        Array.Fill(_channels, ColorRamp.Neutral);
        Vibrance = VibranceSupported ? _defaults.Vibrance : Vibrance;
        Hue = HueSupported ? _defaults.Hue : Hue;
        ReferenceMode = false;
        _loading = false;
        OnColorChannelIndexChanged(ColorChannelIndex);
        await ApplyAsync();
    }

    /// <summary>
    /// Vain's Apply (FUN_1401cc130): writes only what differs from what was read, each part on its own so
    /// one failure leaves the rest applied, then restarts the display driver when Override Scaling changed.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task ApplyAsync()
    {
        if (SelectedDisplay is not { } display || _loaded is not { } loaded || _card is not { } card)
        {
            return;
        }

        var now = Current();
        if (now == loaded)
        {
            _infoBar.ShowInfo("Display settings", "Nothing has changed.");
            return;
        }

        if (BuildInfo.IsDryRunOnly)
        {
            _infoBar.ShowInfo("Display settings", "Dry Run: nothing was written.");
            return;
        }

        IsBusy = true;
        var failures = new List<string>();
        var restartDriver = false;
        try
        {
            using (_status.Begin("Applying display settings..."))
            {
                var scaling = ScalingValue(now);
                if (scaling is { } value && (now.ScalingModeIndex != loaded.ScalingModeIndex || now.ScalingPerformerIndex != loaded.ScalingPerformerIndex))
                {
                    Part(failures, "Scaling", () => NvidiaDisplaySettings.WriteScaling(display.DisplayId, value));
                }

                if (OverrideScalingAvailable && display.DisplayDatabaseKey is { } key && scaling is { } mode &&
                    NvidiaDisplays.ReadScalingConfig(key) is { } blob && ScalingConfig.IsValid(blob))
                {
                    var bits = ScalingConfig.OverrideBitsFor(mode, now.OverrideScaling);
                    if (ScalingConfig.OverrideBits(blob) != bits)
                    {
                        restartDriver = Part(failures, "Override Scaling Mode", () => NvidiaDisplaySettings.WriteScalingConfig(key, ScalingConfig.WithOverrideBits(blob, bits)));
                    }
                }

                if (now.ReferenceMode != loaded.ReferenceMode)
                {
                    Part(failures, "Reference mode", () => WriteReferenceMode(display.DisplayId, now.ReferenceMode));
                }

                if (now.PerformanceCounters != loaded.PerformanceCounters)
                {
                    Part(failures, "GPU performance counter access", () => _machine.Write(
                        new RegistryLocation(RegistryHive.LocalMachine, PerformanceCountersKey, PerformanceCountersValue),
                        RegistryValue.DWord(now.PerformanceCounters ? 0 : 1)));
                }

                if ((now.ColorFormatIndex, now.DynamicRangeIndex, now.ColorDepthIndex) != (loaded.ColorFormatIndex, loaded.DynamicRangeIndex, loaded.ColorDepthIndex) &&
                    now.ColorFormatIndex >= 0 && now.DynamicRangeIndex >= 0 && now.ColorDepthIndex >= 0)
                {
                    Part(failures, "Colour format", () => NvidiaDisplaySettings.WriteColor(
                        display.DisplayId,
                        new(FormatValues[now.ColorFormatIndex], RangeValues[now.DynamicRangeIndex], DepthValues[now.ColorDepthIndex])));
                }

                if (!now.Channels.SequenceEqual(loaded.Channels))
                {
                    Part(failures, "Brightness/Contrast/Gamma", () => NvidiaDisplaySettings.WriteColorTable(display.DisplayId, now.Channels[0], now.Channels[1], now.Channels[2]));
                }

                if (VibranceSupported && now.Vibrance != loaded.Vibrance)
                {
                    Part(failures, "Digital Vibrance", () => NvidiaDisplaySettings.WriteVibrance(display.Handle, (int)Math.Round(now.Vibrance)));
                }

                if (HueSupported && now.Hue != loaded.Hue)
                {
                    Part(failures, "Hue", () => NvidiaDisplaySettings.WriteHue(display.Handle, (int)Math.Round(now.Hue)));
                }
            }

            if (restartDriver)
            {
                using (_status.Begin("Restarting the display driver to apply the changes..."))
                {
                    await WindowsGpuCards.RestartAsync(card);
                }
            }

            Read(display);
            if (failures.Count > 0)
            {
                await _dialogs.ShowErrorAsync(
                    "Display settings",
                    $"{string.Join(", ", failures)} could not be applied; the other settings were applied. The app log has the details.");
            }
            else if (restartDriver && OverrideScaling != now.OverrideScaling)
            {
                _infoBar.ShowWarning("Display settings", "The display driver restarted, but did not keep \"Override Scaling Mode\" - the switch shows what it runs now.");
            }
            else
            {
                _infoBar.ShowSuccess("Display settings", restartDriver ? "Display driver restarted - settings applied." : "Display settings applied.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Applying display settings failed");
            await _dialogs.ShowErrorAsync("Display settings", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // One part of Apply: false, with its name noted, when it fails.
    private bool Part(List<string> failures, string name, Action write)
    {
        try
        {
            write();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Applying {Setting} failed", name);
            failures.Add(name);
            return false;
        }
    }

    private static int? ScalingValue(Snapshot state) =>
        state.ScalingModeIndex < 0 || state.ScalingPerformerIndex < 0
            ? null
            : NvidiaScaling.ToNvScaling((ScalingMode)state.ScalingModeIndex, (ScalingPerformer)state.ScalingPerformerIndex);

    private static IEnumerable<string> ReferenceDevices(uint displayId)
    {
        using var devices = Registry.CurrentUser.OpenSubKey(Core.Gpus.ReferenceMode.DevicesKey, writable: false);
        return Core.Gpus.ReferenceMode.Devices(displayId, devices?.GetSubKeyNames() ?? []);
    }

    // FUN_1401b6e10: the first device that holds the value tells.
    private bool ReadReferenceMode(uint displayId)
    {
        foreach (var device in ReferenceDevices(displayId))
        {
            if (_machine.Read(new RegistryLocation(RegistryHive.CurrentUser, $@"{Core.Gpus.ReferenceMode.DevicesKey}\{device}\Color", Core.Gpus.ReferenceMode.ValueName))
                is RegistryValue { Kind: RegistryValueKind.DWord, Data: int value })
            {
                return Core.Gpus.ReferenceMode.IsOn(value);
            }
        }

        return false;
    }

    private void WriteReferenceMode(uint displayId, bool on)
    {
        foreach (var device in ReferenceDevices(displayId))
        {
            _machine.Write(
                new RegistryLocation(RegistryHive.CurrentUser, $@"{Core.Gpus.ReferenceMode.DevicesKey}\{device}\Color", Core.Gpus.ReferenceMode.ValueName),
                RegistryValue.DWord(Core.Gpus.ReferenceMode.ValueFor(on)));
        }
    }

    private T Try<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reading a display setting failed");
            return fallback;
        }
    }

    private Snapshot Current() => new(
        ColorFormatIndex, DynamicRangeIndex, ColorDepthIndex, ScalingModeIndex, ScalingPerformerIndex,
        OverrideScaling, PerformanceCounters, Hdr10, [.. _channels], Vibrance, Hue, ReferenceMode);

    /// <summary>What the tab shows, to tell what Apply has to write and what Revert puts back.</summary>
    private sealed record Snapshot(
        int ColorFormatIndex,
        int DynamicRangeIndex,
        int ColorDepthIndex,
        int ScalingModeIndex,
        int ScalingPerformerIndex,
        bool OverrideScaling,
        bool PerformanceCounters,
        bool Hdr10,
        ChannelAdjustment[] Channels,
        double Vibrance,
        double Hue,
        bool ReferenceMode)
    {
        public bool Equals(Snapshot? other) =>
            other is not null &&
            (ColorFormatIndex, DynamicRangeIndex, ColorDepthIndex, ScalingModeIndex, ScalingPerformerIndex, OverrideScaling, PerformanceCounters, Hdr10, Vibrance, Hue, ReferenceMode) ==
            (other.ColorFormatIndex, other.DynamicRangeIndex, other.ColorDepthIndex, other.ScalingModeIndex, other.ScalingPerformerIndex, other.OverrideScaling, other.PerformanceCounters, other.Hdr10, other.Vibrance, other.Hue, other.ReferenceMode) &&
            Channels.SequenceEqual(other.Channels);

        public override int GetHashCode() => HashCode.Combine(ColorFormatIndex, ScalingModeIndex, Vibrance, Hue);
    }
}
