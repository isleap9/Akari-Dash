using AkariDash.Core.Gpus;
using Xunit;

namespace AkariDash.Tests;

/// <summary>
/// Vain Toolbox's Scaling Mode and Perform Scaling On, against NV_SCALING in nvapi.h. The pairs are Vain's
/// table at 0x140391440 (name, value on the GPU, value on the display) and its reverse in FUN_1401c8190.
/// </summary>
public class NvidiaScalingTests
{
    [Theory]
    [InlineData(ScalingMode.AspectRatio, ScalingPerformer.Gpu, 5)]
    [InlineData(ScalingMode.AspectRatio, ScalingPerformer.Display, 6)]
    [InlineData(ScalingMode.FullScreen, ScalingPerformer.Gpu, 2)]
    [InlineData(ScalingMode.FullScreen, ScalingPerformer.Display, 1)]
    [InlineData(ScalingMode.NoScaling, ScalingPerformer.Gpu, 3)]
    [InlineData(ScalingMode.NoScaling, ScalingPerformer.Display, 7)]
    [InlineData(ScalingMode.IntegerScaling, ScalingPerformer.Gpu, 8)]
    public void Each_pair_is_one_nv_scaling_value_and_back(ScalingMode mode, ScalingPerformer performer, int value)
    {
        Assert.Equal(value, NvidiaScaling.ToNvScaling(mode, performer));
        Assert.Equal((mode, performer), NvidiaScaling.FromNvScaling(value));
    }

    [Fact]
    public void Integer_scaling_is_done_on_the_gpu_even_when_the_display_is_picked()
    {
        Assert.Equal(8, NvidiaScaling.ToNvScaling(ScalingMode.IntegerScaling, ScalingPerformer.Display));
    }

    // FUN_1401b3800: 1, 6 and 7 send a smaller mode for the display to scale; moving across that line
    // asks the driver for a new timing (NV_TIMING_OVERRIDE_AUTO).
    [Theory]
    [InlineData(5, 6, true)]
    [InlineData(1, 2, true)]
    [InlineData(7, 3, true)]
    [InlineData(5, 2, false)]
    [InlineData(1, 7, false)]
    [InlineData(8, 8, false)]
    public void Moving_scaling_between_gpu_and_display_needs_a_new_timing(int from, int to, bool needed)
    {
        Assert.Equal(needed, NvidiaScaling.NeedsNewTiming(from, to));
    }

    [Fact]
    public void The_driver_default_is_no_pair()
    {
        // NV_SCALING_DEFAULT: nothing set, so neither box shows a choice.
        Assert.Null(NvidiaScaling.FromNvScaling(0));
    }

    [Fact]
    public void The_modes_read_as_vain_names_them()
    {
        Assert.Equal(["Aspect ratio", "Full-screen", "No scaling", "Integer scaling"], NvidiaScaling.ModeNames);
        Assert.Equal(["GPU", "Display"], NvidiaScaling.PerformerNames);
    }
}
