namespace AkariDash.Core.Gpus;

/// <summary>Scaling Mode on Vain Toolbox's Settings tab, in its order.</summary>
public enum ScalingMode
{
    AspectRatio,
    FullScreen,
    NoScaling,
    IntegerScaling,
}

/// <summary>Perform Scaling On: whether the GPU or the display stretches the picture.</summary>
public enum ScalingPerformer
{
    Gpu,
    Display,
}

/// <summary>
/// Scaling Mode and Perform Scaling On as one NV_SCALING value (nvapi.h), the way Vain Toolbox pairs them
/// (its table at 0x140391440 and FUN_1401c8190). Integer scaling exists only on the GPU.
/// </summary>
public static class NvidiaScaling
{
    public static IReadOnlyList<string> ModeNames { get; } = ["Aspect ratio", "Full-screen", "No scaling", "Integer scaling"];

    public static IReadOnlyList<string> PerformerNames { get; } = ["GPU", "Display"];

    public static int ToNvScaling(ScalingMode mode, ScalingPerformer performer) => (mode, performer) switch
    {
        (ScalingMode.AspectRatio, ScalingPerformer.Gpu) => 5,
        (ScalingMode.AspectRatio, ScalingPerformer.Display) => 6,
        (ScalingMode.FullScreen, ScalingPerformer.Gpu) => 2,
        (ScalingMode.FullScreen, ScalingPerformer.Display) => 1,
        (ScalingMode.NoScaling, ScalingPerformer.Gpu) => 3,
        (ScalingMode.NoScaling, ScalingPerformer.Display) => 7,
        _ => 8,
    };

    /// <summary>
    /// Whether going from <paramref name="from"/> to <paramref name="to"/> moves the scaling between the GPU
    /// and the display (1, 6 and 7 leave it to the display), when Vain lets the driver pick a new timing.
    /// </summary>
    public static bool NeedsNewTiming(int from, int to) => ScaledByDisplay(from) != ScaledByDisplay(to);

    private static bool ScaledByDisplay(int value) => value is 1 or 6 or 7;

    /// <summary>The pair an NV_SCALING value stands for; null for the driver default and values Vain has no row for.</summary>
    public static (ScalingMode Mode, ScalingPerformer Performer)? FromNvScaling(int value) => value switch
    {
        5 => (ScalingMode.AspectRatio, ScalingPerformer.Gpu),
        6 => (ScalingMode.AspectRatio, ScalingPerformer.Display),
        2 => (ScalingMode.FullScreen, ScalingPerformer.Gpu),
        1 => (ScalingMode.FullScreen, ScalingPerformer.Display),
        3 => (ScalingMode.NoScaling, ScalingPerformer.Gpu),
        7 => (ScalingMode.NoScaling, ScalingPerformer.Display),
        8 => (ScalingMode.IntegerScaling, ScalingPerformer.Gpu),
        _ => null,
    };
}
