using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Xunit;

namespace AkariDash.Tests;

/// <summary>The Gaming Tweaks added from Winhance's MIT snapshot (issue #22).</summary>
public class GamingWinhanceTests
{
    private static DeclaredTweak Find(string id) => TweakCatalog.All.Single(tweak => tweak.Id == id);

    [Fact]
    public void Nvidia_sharpening_is_only_offered_on_pcs_with_an_nvidia_card()
    {
        Assert.Equal(GpuVendor.Nvidia, Find("gaming.nvidia-sharpening").RequiresGpu);
    }

    [Fact]
    public void Gpu_priority_counts_a_missing_value_as_windows_high_default()
    {
        var tweak = Find("gaming.games-gpu-priority");

        Assert.Equal(RegistryValue.DWord(8), Assert.Single(tweak.Targets).AbsentMeans);
        Assert.Null(tweak.Recommended);
    }

    [Theory]
    [InlineData("gaming.filter-keys-shortcut", "126", "2")]
    [InlineData("gaming.toggle-keys-shortcut", "62", "34")]
    [InlineData("gaming.mouse-keys-shortcut", "62", "130")]
    [InlineData("gaming.high-contrast-shortcut", "126", "4194")]
    public void Accessibility_shortcuts_switch_the_flags_string(string id, string on, string off)
    {
        var tweak = Find(id);
        var target = Assert.Single(tweak.Targets);

        Assert.Equal(RegistryValue.String(on), tweak.Options.Single(option => option.Id == "on").Values[target]);
        Assert.Equal(RegistryValue.String(off), tweak.Options.Single(option => option.Id == "off").Values[target]);
        Assert.Equal(RegistryValue.String(on), target.AbsentMeans);
    }

    [Fact]
    public void Settings_that_do_nothing_or_need_a_single_bit_are_left_out()
    {
        var names = TweakCatalog.All.SelectMany(tweak => tweak.Targets).Select(target => target.Location).OfType<RegistryLocation>().Select(location => location.Name);

        Assert.DoesNotContain("CompositionPolicy", names);
        Assert.DoesNotContain("ServicesPipeTimeout", names);
        Assert.DoesNotContain("VRROptimizeEnable", names);
        Assert.DoesNotContain("UserPreferencesMask", names);
    }
}
