using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class TweakCatalogTests
{
    public static TheoryData<string> TweakIds => new(TweakCatalog.All.Select(tweak => tweak.Id));

    private static DeclaredTweak Find(string id) => TweakCatalog.All.Single(tweak => tweak.Id == id);

    [Theory]
    [MemberData(nameof(TweakIds))]
    public void Every_option_gives_a_value_for_every_target(string id)
    {
        var tweak = Find(id);

        Assert.All(tweak.Options, option => Assert.All(tweak.Targets, target => Assert.True(option.Values.ContainsKey(target))));
    }

    [Theory]
    [MemberData(nameof(TweakIds))]
    public void Recommended_option_is_one_of_the_tweaks_own_options(string id)
    {
        var tweak = Find(id);

        if (tweak.Recommended is { } recommended)
        {
            Assert.Contains(recommended, tweak.Options);
        }
    }

    [Fact]
    public void SysMain_takes_effect_after_restart_because_a_running_service_keeps_running()
    {
        Assert.Equal(Activation.AfterRestart, Find("services.sysmain").Activation);
    }

    [Theory]
    [MemberData(nameof(TweakIds))]
    public void Every_tweak_has_at_least_two_options_with_unique_ids(string id)
    {
        var tweak = Find(id);

        Assert.True(tweak.Options.Count >= 2, $"{id} has fewer than two Options");
        Assert.Equal(tweak.Options.Count, tweak.Options.Select(option => option.Id).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(TweakIds))]
    public void Every_tweak_has_a_title_description_and_group(string id)
    {
        var tweak = Find(id);

        Assert.False(string.IsNullOrWhiteSpace(tweak.Title));
        Assert.False(string.IsNullOrWhiteSpace(tweak.Description));
        Assert.False(string.IsNullOrWhiteSpace(tweak.Group));
    }

    [Theory]
    [InlineData("gaming.game-captures")]
    [InlineData("gaming.mouse-acceleration")]
    [InlineData("gaming.sticky-keys-shortcut")]
    [InlineData("gaming.game-bar-controller-button")]
    [InlineData("gaming.background-apps")]
    [InlineData("gaming.menu-show-delay")]
    [InlineData("gaming.window-animations")]
    [InlineData("gaming.taskbar-animations")]
    [InlineData("gaming.peek")]
    [InlineData("gaming.transparency")]
    [InlineData("gaming.drag-full-windows")]
    [InlineData("gaming.windowed-game-optimizations")]
    [InlineData("gaming.fullscreen-optimizations")]
    [InlineData("gaming.system-responsiveness")]
    [InlineData("gaming.network-throttling")]
    [InlineData("gaming.games-priority")]
    [InlineData("gaming.startup-delay")]
    [InlineData("gaming.storage-sense")]
    [InlineData("gaming.automatic-maintenance")]
    public void Taste_based_tweaks_have_no_recommended_option(string id)
    {
        Assert.Null(Find(id).Recommended);
    }

    [Theory]
    [InlineData("gaming.background-recording", "off")]
    [InlineData("gaming.hardware-gpu-scheduling", "on")]
    [InlineData("gaming.power-throttling", "off")]
    public void Performance_tweaks_recommend(string id, string option)
    {
        Assert.Equal(option, Find(id).Recommended?.Id);
    }

    [Theory]
    [InlineData("gaming.hardware-gpu-scheduling", Activation.AfterRestart)]
    [InlineData("gaming.power-throttling", Activation.AfterRestart)]
    [InlineData("gaming.mouse-acceleration", Activation.AfterSignOut)]
    [InlineData("gaming.sticky-keys-shortcut", Activation.AfterSignOut)]
    [InlineData("gaming.menu-show-delay", Activation.AfterSignOut)]
    [InlineData("gaming.window-animations", Activation.AfterSignOut)]
    [InlineData("gaming.taskbar-animations", Activation.AfterSignOut)]
    [InlineData("gaming.peek", Activation.AfterSignOut)]
    [InlineData("gaming.drag-full-windows", Activation.AfterSignOut)]
    [InlineData("gaming.system-responsiveness", Activation.AfterRestart)]
    [InlineData("gaming.network-throttling", Activation.AfterRestart)]
    [InlineData("gaming.games-priority", Activation.AfterRestart)]
    [InlineData("gaming.startup-delay", Activation.AfterSignOut)]
    [InlineData("gaming.automatic-maintenance", Activation.AfterRestart)]
    public void Tweaks_that_windows_reads_only_at_sign_in_or_startup_declare_their_activation(string id, Activation activation)
    {
        Assert.Equal(activation, Find(id).Activation);
    }

    [Fact]
    public void Hardware_gpu_scheduling_lets_windows_decide_by_deleting_the_value()
    {
        var tweak = Find("gaming.hardware-gpu-scheduling");
        var target = Assert.Single(tweak.Targets);

        Assert.Null(tweak.Options.Single(option => option.Id == "windows-decides").Values[target]);
    }

    [Theory]
    [InlineData("MouseSpeed", "1")]
    [InlineData("MouseThreshold1", "6")]
    [InlineData("MouseThreshold2", "10")]
    public void Mouse_acceleration_on_writes_the_windows_defaults(string name, string windowsDefault)
    {
        var tweak = Find("gaming.mouse-acceleration");
        var target = tweak.Targets.Single(target => target.Location == new RegistryLocation(RegistryHive.CurrentUser, @"Control Panel\Mouse", name));

        Assert.Equal(RegistryValue.String(windowsDefault), tweak.Options.Single(option => option.Id == "on").Values[target]);
    }

    [Fact]
    public void Tweak_ids_are_unique()
    {
        Assert.Equal(TweakCatalog.All.Count, TweakCatalog.All.Select(tweak => tweak.Id).Distinct().Count());
    }

    [Fact]
    public void Processor_scheduling_offers_every_quantum_length_type_and_boost_combination()
    {
        var tweak = Find("gaming.processor-scheduling");
        var target = Assert.Single(tweak.Targets);

        // Each of the three 2-bit fields takes Default/first/second (0-2), as in AkariOS-Ultimate's tuner.
        var values = tweak.Options.Select(option => (int)((RegistryValue)option.Values[target]!).Data).ToList();
        Assert.Equal(27, values.Distinct().Count());
        Assert.All(values, value => Assert.All(new[] { value >> 4, (value >> 2) & 3, value & 3 }, field => Assert.InRange(field, 0, 2)));
    }

    [Fact]
    public void Processor_scheduling_labels_options_like_the_tuner_and_recommends_0x26()
    {
        var tweak = Find("gaming.processor-scheduling");

        Assert.Equal("Default · Default · High (0x02)", tweak.Options.Single(option => option.Id == "0x02").Label);
        Assert.Equal("Short · Variable · High (0x26)", tweak.Recommended?.Label);
        Assert.Equal("Long · Fixed · None (0x18)", tweak.Options.Single(option => option.Id == "0x18").Label);
    }

    [Fact]
    public void Services_and_scheduled_tasks_live_only_on_the_services_page()
    {
        var elsewhere = TweakCatalog.All.Where(tweak => tweak.Category != Category.Services);

        Assert.All(elsewhere, tweak => Assert.All(tweak.Targets, target => Assert.IsType<RegistryLocation>(target.Location)));
    }

    [Theory]
    [MemberData(nameof(TweakIds))]
    public void Every_option_value_is_the_kind_its_target_holds(string id)
    {
        var tweak = Find(id);

        Assert.All(tweak.Options, option => Assert.All(option.Values, pair => Assert.True(
            pair.Value is null || (pair.Key.Location, pair.Value) is
                (RegistryLocation, RegistryValue) or (ServiceLocation, ServiceStartValue) or (ScheduledTaskLocation, TaskEnabledValue),
            $"{option.Id}: {pair.Key.Location} cannot hold {pair.Value}")));
    }

    [Fact]
    public void Game_mode_recommends_on()
    {
        Assert.Equal("on", Find("gaming.game-mode").Recommended?.Id);
    }

    [Fact]
    public void Privacy_has_tweaks_in_several_groups()
    {
        var privacy = TweakCatalog.All.Where(tweak => tweak.Category == Category.Privacy).ToList();

        Assert.True(privacy.Select(tweak => tweak.Group).Distinct().Count() >= 3);
        Assert.All(privacy, tweak => Assert.StartsWith("privacy.", tweak.Id));
    }

    [Fact]
    public void Only_automatic_app_installs_has_a_recommended_option_in_privacy()
    {
        // Recommended Options are for performance; only the silent app installs cost background work.
        var recommended = Assert.Single(TweakCatalog.All, tweak => tweak.Category == Category.Privacy && tweak.Recommended is not null);

        Assert.Equal("privacy.automatic-app-installs", recommended.Id);
        Assert.Equal("off", recommended.Recommended?.Id);
    }

    [Fact]
    public void Feedback_frequency_automatically_deletes_both_values_so_windows_decides_again()
    {
        var tweak = Find("privacy.feedback-frequency");
        var automatically = tweak.Options.Single(option => option.Id == "automatically");

        Assert.Equal(2, tweak.Targets.Count);
        Assert.All(tweak.Targets, target => Assert.Null(automatically.Values[target]));
    }

    [Theory]
    [InlineData("privacy.diagnostic-data")]
    [InlineData("privacy.activity-history")]
    [InlineData("privacy.web-search-in-start")]
    public void Policy_tweaks_can_remove_the_policy_again(string id)
    {
        var tweak = Find(id);
        var target = Assert.Single(tweak.Targets);

        Assert.Contains(tweak.Options, option => option.Values[target] is null);
    }

    [Theory]
    [InlineData("gaming.windowed-game-optimizations", "windows-decides")]
    [InlineData("gaming.startup-delay", "windows-default")]
    [InlineData("gaming.storage-sense", "per-user")]
    [InlineData("gaming.automatic-maintenance", "on")]
    public void Options_that_hand_the_choice_back_to_windows_delete_the_value(string id, string option)
    {
        var tweak = Find(id);

        Assert.All(tweak.Targets, target => Assert.Null(tweak.Options.Single(candidate => candidate.Id == option).Values[target]));
    }

    [Fact]
    public void Network_throttling_off_writes_ffffffff()
    {
        var tweak = Find("gaming.network-throttling");
        var target = Assert.Single(tweak.Targets);

        Assert.Equal(RegistryValue.DWord(unchecked((int)0xFFFFFFFF)), tweak.Options.Single(option => option.Id == "off").Values[target]);
        Assert.Equal(RegistryValue.DWord(10), target.AbsentMeans);
    }

    [Fact]
    public void System_responsiveness_counts_a_missing_value_as_the_windows_default_of_20()
    {
        var target = Assert.Single(Find("gaming.system-responsiveness").Targets);

        Assert.Equal(RegistryValue.DWord(20), target.AbsentMeans);
    }

    [Fact]
    public void Fullscreen_optimizations_off_asks_for_exclusive_fullscreen()
    {
        var tweak = Find("gaming.fullscreen-optimizations");
        var off = tweak.Options.Single(option => option.Id == "off");
        var mode = tweak.Targets.Single(target => ((RegistryLocation)target.Location).Name == "GameDVR_FSEBehaviorMode");

        Assert.Equal(4, tweak.Targets.Count);
        Assert.Equal(RegistryValue.DWord(2), off.Values[mode]);
    }

    [Fact]
    public void Games_priority_default_writes_the_windows_values_back()
    {
        var tweak = Find("gaming.games-priority");
        var windowsDefault = tweak.Options.Single(option => option.Id == "windows-default");

        Assert.All(tweak.Targets, target => Assert.Equal(target.AbsentMeans, windowsDefault.Values[target]));
    }
}
