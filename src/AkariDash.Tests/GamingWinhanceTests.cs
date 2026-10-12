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
    public void Search_webview2_off_sets_the_feature_override_and_windows_decides_deletes_it()
    {
        var tweak = Find("gaming.search-webview2");
        var target = Assert.Single(tweak.Targets);

        Assert.Equal(RegistryValue.DWord(1), tweak.Options.Single(option => option.Id == "off").Values[target]);
        Assert.Null(tweak.Options.Single(option => option.Id == "windows-decides").Values[target]);
    }

    [Theory]
    [InlineData("gaming.wallpaper-quality", 85)]
    [InlineData("gaming.svchost-split", 3670016)]
    public void Missing_values_count_as_windows_default(string id, int windowsDefault)
    {
        Assert.Equal(RegistryValue.DWord(windowsDefault), Assert.Single(Find(id).Targets).AbsentMeans);
    }

    [Fact]
    public void Accessibility_shortcuts_are_the_last_section_on_the_gaming_page()
    {
        var gaming = TweakCatalog.All.Where(tweak => tweak.Category == Category.Gaming).ToList();

        Assert.Equal("Accessibility", gaming.Last().Group);
        Assert.Equal(
            ["gaming.sticky-keys-shortcut", "gaming.filter-keys-shortcut", "gaming.toggle-keys-shortcut",
             "gaming.mouse-keys-shortcut", "gaming.high-contrast-shortcut", "gaming.narrator-shortcut"],
            gaming.Where(tweak => tweak.Group == "Accessibility").Select(tweak => tweak.Id));
    }

    [Fact]
    public void Svchost_split_sits_right_after_processor_scheduling()
    {
        var ids = TweakCatalog.All.Select(tweak => tweak.Id).ToList();

        Assert.Equal("Processor", Find("gaming.svchost-split").Group);
        Assert.Equal(ids.IndexOf("gaming.processor-scheduling") + 1, ids.IndexOf("gaming.svchost-split"));
    }

    [Fact]
    public void Svchost_split_is_a_tuner_with_akarios_ultimates_three_presets()
    {
        var tweak = Find("gaming.svchost-split");
        var target = Assert.Single(tweak.Targets);

        Assert.Equal(["windows-default", "match-ram", "never"], tweak.Options.Select(option => option.Id));
        Assert.Equal(["Windows default", "Match RAM", "Never split"], tweak.Options.Select(option => option.Label));
        Assert.Equal("windows-default", tweak.Tuner?.DefaultOption.Id);
        Assert.Equal("KB", tweak.Tuner?.Unit);
        Assert.Equal(RegistryValue.DWord(unchecked((int)0xFFFFFFFF)), tweak.Options[2].Values[target]);
    }

    [Fact]
    public void Match_ram_is_this_pcs_ram_rounded_up_to_whole_gigabytes_in_kilobytes()
    {
        var tweak = Find("gaming.svchost-split");
        var matchRam = (RegistryValue)tweak.Options.Single(option => option.Id == "match-ram").Values[tweak.Targets[0]]!;
        var kilobytes = unchecked((uint)(int)matchRam.Data);

        Assert.Equal(0u, kilobytes % (1024 * 1024));
        Assert.Equal($"RAM: {kilobytes / (1024 * 1024)} GB", tweak.Tuner?.Detail);
    }

    [Fact]
    public void Only_the_svchost_split_is_drawn_as_a_tuner()
    {
        Assert.Equal(["gaming.svchost-split"], TweakCatalog.All.Where(tweak => tweak.Tuner is not null).Select(tweak => tweak.Id));
    }

    [Fact]
    public void Graphics_has_winhances_settings_plus_mpo_in_order()
    {
        Assert.Equal(
            ["gaming.hardware-gpu-scheduling", "gaming.nvidia-sharpening", "gaming.games-gpu-priority", "gaming.windowed-game-optimizations",
             "gaming.vrr-optimizations", "gaming.fullscreen-optimizations", "gaming.mpo", "gaming.mpo-min-fps"],
            TweakCatalog.All.Where(tweak => tweak is { Category: Category.Gaming, Group: "Graphics" }).Select(tweak => tweak.Id));
    }

    [Theory]
    [InlineData("gaming.windowed-game-optimizations", "SwapEffectUpgradeEnable")]
    [InlineData("gaming.vrr-optimizations", "VRROptimizeEnable")]
    public void DirectX_settings_change_only_their_own_entry(string id, string entry)
    {
        var location = Assert.IsType<RegistryEntryLocation>(Assert.Single(Find(id).Targets).Location);

        Assert.Equal(("DirectXUserGlobalSettings", entry), (location.Name, location.Entry));
    }

    [Theory]
    [InlineData("gaming.mpo", "OverlayTestMode", 5)]
    [InlineData("gaming.mpo-min-fps", "OverlayMinFPS", 0)]
    public void Mpo_settings_write_their_dwm_value_and_delete_it_for_windows_default(string id, string name, int off)
    {
        var tweak = Find(id);
        var target = Assert.Single(tweak.Targets);

        Assert.Equal(name, ((RegistryLocation)target.Location).Name);
        Assert.Equal(RegistryValue.DWord(off), tweak.Options.Single(option => option.Id == "off").Values[target]);
        Assert.Null(tweak.Options.Single(option => option.Id == "on").Values[target]);
    }

    [Fact]
    public void Settings_that_do_nothing_are_left_out_and_the_mask_is_never_written_whole()
    {
        var names = TweakCatalog.All.SelectMany(tweak => tweak.Targets).Select(target => target.Location).OfType<RegistryLocation>().Select(location => location.Name);

        Assert.DoesNotContain("CompositionPolicy", names);
        Assert.DoesNotContain("ServicesPipeTimeout", names);
        Assert.DoesNotContain("VRROptimizeEnable", names);
        // UserPreferencesMask effects are single bits (RegistryBitLocation), never the whole value.
        Assert.DoesNotContain("UserPreferencesMask", names);
    }
}
