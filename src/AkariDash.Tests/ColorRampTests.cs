using AkariDash.Core.Gpus;
using Xunit;

namespace AkariDash.Tests;

/// <summary>
/// Brightness, Contrast and Gamma as the 1024-step ramp Vain Toolbox hands the driver (FUN_1401c9c90 and
/// FUN_1401c9d90). Expected values are worked by hand from that formula.
/// </summary>
public class ColorRampTests
{
    [Theory]
    [InlineData(50, 50, 1.0, 0, 0.0)]
    [InlineData(50, 50, 1.0, 1023, 1.0)]
    [InlineData(100, 50, 1.0, 0, 0.2)]        // brightness shifts by (100 - 50) / 250
    [InlineData(0, 50, 1.0, 1023, 0.8)]
    [InlineData(50, 100, 1.0, 0, 0.0)]        // contrast stretches around the middle, then clamps
    [InlineData(50, 100, 1.0, 767, 0.81219)]  // (767/1023 - 0.5) / 0.8 + 0.5
    [InlineData(50, 0, 1.0, 1023, 0.9)]       // (1 - 0.2) * 0.5 + 0.5
    [InlineData(50, 50, 2.0, 511, 0.706761)]  // sqrt(511/1023)
    public void A_step_follows_vains_formula(double brightness, double contrast, double gamma, int index, double expected)
    {
        Assert.Equal(expected, ColorRamp.Value(brightness, contrast, gamma, index), 5);
    }

    [Fact]
    public void The_ramp_is_1024_steps_of_red_green_blue()
    {
        var ramp = ColorRamp.Build(new(100, 50, 1.0), ColorRamp.Neutral, new(0, 50, 1.0));

        Assert.Equal(3072, ramp.Length);
        Assert.Equal(0.2f, ramp[0], 5);   // red at step 0
        Assert.Equal(0f, ramp[1], 5);     // green at step 0
        Assert.Equal(0f, ramp[2], 5);     // blue at step 0
        Assert.Equal(1f, ramp[3069], 5);  // red at step 1023
        Assert.Equal(1f, ramp[3070], 5);  // green at step 1023
        Assert.Equal(0.8f, ramp[3071], 5); // blue at step 1023
    }
}
