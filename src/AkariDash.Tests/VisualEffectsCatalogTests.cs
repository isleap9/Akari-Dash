using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Xunit;

namespace AkariDash.Tests;

/// <summary>The UserPreferencesMask effects and the Visual effects preset (issue #23).</summary>
public class VisualEffectsCatalogTests
{
    private static DeclaredTweak Find(string id) => TweakCatalog.All.Single(tweak => tweak.Id == id);

    [Theory]
    [InlineData("gaming.animate-controls", 4, 0x02)]
    [InlineData("gaming.menu-animation", 0, 0x02)]
    [InlineData("gaming.combo-box-animation", 0, 0x04)]
    [InlineData("gaming.smooth-scroll-lists", 0, 0x08)]
    [InlineData("gaming.fade-menu-items", 1, 0x04)]
    [InlineData("gaming.tooltip-animation", 1, 0x08)]
    [InlineData("gaming.pointer-shadow", 1, 0x20)]
    [InlineData("gaming.window-shadows", 2, 0x04)]
    public void Each_effect_is_one_bit_of_user_preferences_mask(string id, int byteIndex, int mask)
    {
        var bit = Assert.IsType<RegistryBitLocation>(Assert.Single(Find(id).Targets).Location);

        Assert.Equal("UserPreferencesMask", bit.Name);
        Assert.Equal((byteIndex, (byte)mask), (bit.ByteIndex, bit.Mask));
    }

    [Fact]
    public void The_preset_switches_visual_fx_setting_and_all_seventeen_effects()
    {
        var preset = Find("gaming.visual-effects-preset");

        Assert.Equal(18, preset.Targets.Count);
        Assert.Equal(["appearance", "balanced", "performance"], preset.Options.Select(option => option.Id));
        Assert.Equal("Visual effects", TweakCatalog.All.First(tweak => tweak.Group == "Visual effects").Title);
    }

    [Fact]
    public void Best_performance_puts_every_effect_in_its_own_off_option_and_best_appearance_in_on()
    {
        var preset = Find("gaming.visual-effects-preset");
        var performance = preset.Options.Single(option => option.Id == "performance");
        var appearance = preset.Options.Single(option => option.Id == "appearance");

        foreach (var target in preset.Targets.Skip(1))
        {
            var effect = TweakCatalog.All.Single(tweak => tweak != preset && tweak.Targets.Contains(target));
            Assert.Equal(effect.Options.Single(option => option.Id == "off").Values[target], performance.Values[target]);
            Assert.Equal(effect.Options.Single(option => option.Id == "on").Values[target], appearance.Values[target]);
        }
    }
}
