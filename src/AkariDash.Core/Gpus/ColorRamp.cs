namespace AkariDash.Core.Gpus;

/// <summary>One colour channel's Brightness (0-100), Contrast (0-100) and Gamma (0.30-2.80), as Vain Toolbox's sliders hold them.</summary>
public readonly record struct ChannelAdjustment(double Brightness, double Contrast, double Gamma);

/// <summary>
/// Brightness, Contrast and Gamma as the ramp Vain Toolbox hands the driver: 1024 steps of red, green and
/// blue floats between 0 and 1 (its FUN_1401c9d90, each step from FUN_1401c9c90).
/// </summary>
public static class ColorRamp
{
    public const int Steps = 1024;

    /// <summary>50, 50 and 1.0: the ramp that changes nothing.</summary>
    public static ChannelAdjustment Neutral { get; } = new(50, 50, 1.0);

    /// <summary>
    /// One step: contrast stretches the input around the middle, brightness shifts it, gamma bends it,
    /// clamped to 0-1 on the way, all in single precision as Vain computes it.
    /// </summary>
    public static float Value(double brightness, double contrast, double gamma, int index)
    {
        var exponent = Math.Max(0.01, gamma);
        var stretch = Math.Clamp((float)((contrast - 50.0) / 250.0), -0.95f, 0.95f);
        var x = index / 1023.0f - 0.5f;
        x = stretch <= 0 ? (stretch + 1.0f) * x : x / (1.0f - stretch);
        var shifted = Math.Clamp(x + 0.5f + (float)((brightness - 50.0) / 250.0), 0f, 1f);
        return Math.Clamp((float)Math.Pow(shifted, 1.0 / exponent), 0f, 1f);
    }

    /// <summary>The whole ramp, step by step, red then green then blue.</summary>
    public static float[] Build(ChannelAdjustment red, ChannelAdjustment green, ChannelAdjustment blue)
    {
        var ramp = new float[Steps * 3];
        for (var i = 0; i < Steps; i++)
        {
            ramp[i * 3] = Value(red.Brightness, red.Contrast, red.Gamma, i);
            ramp[i * 3 + 1] = Value(green.Brightness, green.Contrast, green.Gamma, i);
            ramp[i * 3 + 2] = Value(blue.Brightness, blue.Contrast, blue.Gamma, i);
        }

        return ramp;
    }
}
