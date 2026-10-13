using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

/// <summary>
/// The Privacy and Security Tweaks that finish Winhance's Privacy &amp; Security page: the
/// ContentDeliveryManager family, the lock screen, Cortana, Narrator, the two app permissions and
/// the settings Winhance keeps in that page's Security group.
/// </summary>
public class PrivacyWinhanceTests
{
    private static DeclaredTweak Find(string id) => TweakCatalog.All.Single(tweak => tweak.Id == id);

    [Theory]
    [InlineData("privacy.content-delivery", "ContentDeliveryAllowed")]
    [InlineData("privacy.subscribed-content", "SubscribedContentEnabled")]
    [InlineData("privacy.feature-suggestions", "FeatureManagementEnabled")]
    [InlineData("privacy.tips-after-updates", "SoftLandingEnabled")]
    [InlineData("privacy.manufacturers-apps", "OemPreInstalledAppsEnabled")]
    [InlineData("privacy.suggested-apps", "PreInstalledAppsEnabled")]
    [InlineData("privacy.suggested-apps-history", "PreInstalledAppsEverEnabled")]
    [InlineData("privacy.windows-spotlight", "RotatingLockScreenEnabled")]
    [InlineData("privacy.timeline-suggestions", "SubscribedContent-353698Enabled")]
    public void Content_delivery_values_are_off_or_on_and_count_as_on_when_missing(string id, string name)
    {
        var tweak = Find(id);
        var target = Assert.Single(tweak.Targets);

        Assert.Equal(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", name),
            target.Location);
        Assert.Equal(RegistryValue.DWord(1), target.AbsentMeans);
        Assert.Equal(RegistryValue.DWord(0), tweak.Options.Single(option => option.Id == "off").Values[target]);
        Assert.Equal(RegistryValue.DWord(1), tweak.Options.Single(option => option.Id == "on").Values[target]);
    }

    // The one value of the family a fresh install leaves off: the lock screen shows a single picture
    // until you pick a slideshow, so a missing value must not read as on.
    [Fact]
    public void The_lock_screen_slideshow_counts_as_off_when_its_value_is_missing()
    {
        Assert.Equal(RegistryValue.DWord(0), Assert.Single(Find("privacy.lock-screen-slideshow").Targets).AbsentMeans);
    }

    [Fact]
    public void The_lock_screen_group_holds_spotlight_tips_and_the_slideshow()
    {
        Assert.Equal(
            ["privacy.windows-spotlight", "privacy.lock-screen-tips", "privacy.lock-screen-slideshow"],
            TweakCatalog.All.Where(tweak => tweak.Group == "Lock screen").Select(tweak => tweak.Id));
    }

    // AllowCortana reads the other way round from the policies next to it: 0 is what blocks Cortana,
    // and 1 would allow it, so writing 1 to block would switch it back on.
    [Fact]
    public void Cortana_is_blocked_by_a_zero_and_handed_back_by_deleting_the_policy()
    {
        var tweak = Find("privacy.cortana");

        Assert.Equal(2, tweak.Targets.Count);
        Assert.All(tweak.Targets, target =>
        {
            Assert.Equal(RegistryValue.DWord(0), tweak.Options.Single(option => option.Id == "off").Values[target]);
            Assert.Null(tweak.Options.Single(option => option.Id == "not-blocked").Values[target]);
        });
    }

    [Theory]
    [InlineData("privacy.onedrive-folder-backup", "KFMBlockOptIn")]
    [InlineData("privacy.error-reporting", "Disabled")]
    public void Machine_and_user_policies_are_both_written_and_both_removable(string id, string name)
    {
        var tweak = Find(id);

        Assert.Equal(
            [RegistryHive.LocalMachine, RegistryHive.CurrentUser],
            tweak.Targets.Select(target => ((RegistryLocation)target.Location).Hive));
        Assert.All(tweak.Targets, target =>
        {
            Assert.Equal(name, ((RegistryLocation)target.Location).Name);
            Assert.Equal(RegistryValue.DWord(1), tweak.Options.Single(option => option.Id == "off").Values[target]);
            Assert.Null(tweak.Options.Single(option => option.Id == "not-blocked").Values[target]);
        });
    }

    [Theory]
    [InlineData("privacy.narrator-online-services", "OnlineServicesEnabled")]
    [InlineData("privacy.narrator-scripting", "ScriptingEnabled")]
    public void Narrator_values_live_under_no_roam_and_count_as_on_when_missing(string id, string name)
    {
        var target = Assert.Single(Find(id).Targets);

        Assert.Equal(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Narrator\NoRoam", name), target.Location);
        Assert.Equal(RegistryValue.DWord(1), target.AbsentMeans);
    }

    [Theory]
    [InlineData("privacy.account-info", "userAccountInformation")]
    [InlineData("privacy.app-diagnostics", "appDiagnostics")]
    public void The_new_app_permissions_switch_their_consent_store_value(string id, string consentKey)
    {
        var tweak = Find(id);
        var target = Assert.Single(tweak.Targets);

        Assert.Equal(
            new RegistryLocation(
                RegistryHive.LocalMachine,
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{consentKey}",
                "Value"),
            target.Location);
        Assert.Equal(RegistryValue.String("Allow"), target.AbsentMeans);
        Assert.Equal(RegistryValue.String("Deny"), tweak.Options.Single(option => option.Id == "off").Values[target]);
    }

    // The five levels of Windows' own UAC slider, as Microsoft documents the two values behind it.
    [Theory]
    [InlineData("credentials", 1, 1)]
    [InlineData("always", 2, 1)]
    [InlineData("apps", 5, 1)]
    [InlineData("apps-no-dim", 5, 0)]
    [InlineData("never", 0, 0)]
    public void Uac_levels_write_the_consent_prompt_and_the_secure_desktop_together(
        string optionId, int behavior, int secureDesktop)
    {
        var tweak = Find("privacy.uac-level");
        var option = tweak.Options.Single(candidate => candidate.Id == optionId);

        Assert.Equal(RegistryValue.DWord(behavior), option.Values[tweak.Targets[0]]);
        Assert.Equal(RegistryValue.DWord(secureDesktop), option.Values[tweak.Targets[1]]);
    }

    // AkariOS-Ultimate's own UAC Tweak is one value, EnableLUA = 0, which switches UAC off rather
    // than changing what it asks. It is the last Option here, and the only one that needs a restart.
    [Fact]
    public void The_akarios_ultimate_option_switches_uac_itself_off_after_a_restart()
    {
        var tweak = Find("privacy.uac-level");
        var off = tweak.Options.Single(option => option.Id == "off");
        var enableLua = tweak.Targets.Single(target => ((RegistryLocation)target.Location).Name == "EnableLUA");

        Assert.Equal("UAC off (AkariOS-Ultimate)", off.Label);
        Assert.Equal(RegistryValue.DWord(0), off.Values[enableLua]);
        Assert.Equal(Activation.AfterRestart, tweak.ActivationOf(off));

        // The prompt values go to their quietest, as Never notify does, so the two differ only in
        // whether UAC is still running.
        var never = tweak.Options.Single(option => option.Id == "never");
        Assert.Equal(never.Values[tweak.Targets[0]], off.Values[tweak.Targets[0]]);
        Assert.Equal(never.Values[tweak.Targets[1]], off.Values[tweak.Targets[1]]);
        Assert.NotEqual(never.Values[enableLua], off.Values[enableLua]);
    }

    // Every level switches UAC back on, so picking one undoes the AkariOS-Ultimate Option, and a
    // machine that already has UAC off reads as that Option instead of Custom.
    [Fact]
    public void Every_other_uac_level_leaves_uac_itself_on_and_takes_effect_at_once()
    {
        var tweak = Find("privacy.uac-level");
        var enableLua = tweak.Targets.Single(target => ((RegistryLocation)target.Location).Name == "EnableLUA");

        Assert.Equal(RegistryValue.DWord(1), enableLua.AbsentMeans);
        Assert.All(tweak.Options.Where(option => option.Id != "off"), option =>
        {
            Assert.Equal(RegistryValue.DWord(1), option.Values[enableLua]);
            Assert.Equal(Activation.Immediately, tweak.ActivationOf(option));
        });
    }

    [Fact]
    public void The_description_says_the_akarios_ultimate_option_goes_further()
    {
        var description = Find("privacy.uac-level").Description;

        Assert.Contains("AkariOS-Ultimate", description);
        Assert.Contains("goes further", description);
        Assert.Contains("restart", description);
        Assert.Contains("Store apps", description);
    }

    [Fact]
    public void Uac_counts_a_machine_with_no_values_as_the_level_windows_ships_with()
    {
        var tweak = Find("privacy.uac-level");

        Assert.Equal(RegistryValue.DWord(5), tweak.Targets[0].AbsentMeans);
        Assert.Equal(RegistryValue.DWord(1), tweak.Targets[1].AbsentMeans);
        Assert.Contains(tweak.Options, option => option.Id == "apps");
    }

    [Theory]
    [InlineData("privacy.automatic-device-encryption", 0)]
    [InlineData("privacy.remote-assistance", 1)]
    // A missing "Allow..." value counts as allowed: the VM has one of the two at 1 with the other
    // key absent, and counting absent as 0 made that stock machine read Custom.
    [InlineData("privacy.wifi-sense", 1)]
    public void The_security_group_tweaks_count_a_missing_value_as_what_windows_does(string id, int absent)
    {
        Assert.All(Find(id).Targets, target => Assert.Equal(RegistryValue.DWord(absent), target.AbsentMeans));
    }

    [Fact]
    public void Wifi_sense_switches_both_of_its_policy_manager_values()
    {
        var tweak = Find("privacy.wifi-sense");

        Assert.Equal(
            [@"SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting",
             @"SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots"],
            tweak.Targets.Select(target => ((RegistryLocation)target.Location).Key));
        Assert.All(tweak.Targets, target => Assert.Equal("Value", ((RegistryLocation)target.Location).Name));
    }

    // Winhance writes a Group Policy next to the switch in Settings for these. The policy is its own
    // Option rather than part of Off, because a PC where someone only used the switch in Settings has
    // to keep reading as Off instead of turning into Custom.
    [Theory]
    [InlineData("privacy.advertising-id", "DisabledByGroupPolicy", 1)]
    [InlineData("privacy.tailored-experiences", "DisableTailoredExperiencesWithDiagnosticData", 1)]
    [InlineData("privacy.online-speech", "AllowInputPersonalization", 0)]
    [InlineData("privacy.location", "DisableLocation", 1)]
    public void Off_leaves_the_policy_alone_and_off_and_locked_writes_it(string id, string policyName, int offValue)
    {
        var tweak = Find(id);
        var policies = tweak.Targets
            .Where(target => ((RegistryLocation)target.Location).Name == policyName)
            .ToList();

        // One copy for the machine and one for the user, as Winhance writes them.
        Assert.Equal(
            [RegistryHive.LocalMachine, RegistryHive.CurrentUser],
            policies.Select(target => ((RegistryLocation)target.Location).Hive));

        foreach (var optionId in new[] { "off", "on" })
        {
            var option = tweak.Options.Single(candidate => candidate.Id == optionId);
            Assert.All(policies, policy => Assert.Null(option.Values[policy]));
        }

        var locked = tweak.Options.Single(option => option.Id == "off-locked");
        Assert.All(policies, policy => Assert.Equal(RegistryValue.DWord(offValue), locked.Values[policy]));
    }

    // Off and "Off, and locked" must write the same thing to the setting itself: only the policy differs.
    [Theory]
    [InlineData("privacy.advertising-id")]
    [InlineData("privacy.tailored-experiences")]
    [InlineData("privacy.online-speech")]
    [InlineData("privacy.location")]
    public void The_locked_option_switches_the_setting_off_the_same_way_off_does(string id)
    {
        var tweak = Find(id);
        var off = tweak.Options.Single(option => option.Id == "off");
        var locked = tweak.Options.Single(option => option.Id == "off-locked");
        var settings = tweak.Targets.Where(target => off.Values[target] is not null);

        Assert.NotEmpty(settings);
        Assert.All(settings, target => Assert.Equal(off.Values[target], locked.Values[target]));
    }

    // The permissions Winhance has no policy for keep their two Options, so no empty choice appears.
    [Theory]
    [InlineData("privacy.camera")]
    [InlineData("privacy.microphone")]
    [InlineData("privacy.account-info")]
    [InlineData("privacy.app-diagnostics")]
    public void App_permissions_without_a_policy_stay_off_or_on(string id)
    {
        Assert.Equal(["off", "on"], Find(id).Options.Select(option => option.Id));
    }

    [Fact]
    public void Feedback_requests_can_be_set_to_never_with_or_without_the_policy()
    {
        var tweak = Find("privacy.feedback-frequency");
        var policies = tweak.Targets
            .Where(target => ((RegistryLocation)target.Location).Name == "DoNotShowFeedbackNotifications")
            .ToList();
        var count = tweak.Targets.Single(target => ((RegistryLocation)target.Location).Name == "NumberOfSIUFInPeriod");

        Assert.Equal(2, policies.Count);
        Assert.All(policies, policy => Assert.Null(tweak.Options.Single(option => option.Id == "never").Values[policy]));
        Assert.All(policies, policy => Assert.Equal(
            RegistryValue.DWord(1), tweak.Options.Single(option => option.Id == "never-locked").Values[policy]));
        Assert.All(["never", "never-locked"], optionId => Assert.Equal(
            RegistryValue.DWord(0), tweak.Options.Single(option => option.Id == optionId).Values[count]));
    }

    // Minimum sets the documented policy for the machine and the user; As set in Settings deletes
    // both, so a PC nothing has been applied on reads as that. The older
    // CurrentVersion\Policies\DataCollection pair is deliberately not a target: the VM found Windows
    // keeping those values itself, so owning them would leave this Tweak Custom after an Undo.
    [Fact]
    public void Diagnostic_data_writes_the_telemetry_policy_for_the_machine_and_the_user()
    {
        var tweak = Find("privacy.diagnostic-data");
        var minimum = tweak.Options.Single(option => option.Id == "minimum");

        Assert.Equal(
            [RegistryHive.LocalMachine, RegistryHive.CurrentUser],
            tweak.Targets.Select(target => ((RegistryLocation)target.Location).Hive));
        Assert.All(tweak.Targets, target =>
        {
            var location = (RegistryLocation)target.Location;

            Assert.Equal(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", location.Key);
            Assert.Equal("AllowTelemetry", location.Name);
            Assert.Equal(RegistryValue.DWord(0), minimum.Values[target]);
            Assert.Null(tweak.Options.Single(option => option.Id == "settings-decides").Values[target]);
        });
    }

    [Fact]
    public void Diagnostic_data_does_not_own_the_values_windows_keeps_itself()
    {
        var owned = TweakCatalog.All
            .SelectMany(tweak => tweak.Targets)
            .Select(target => target.Location)
            .OfType<RegistryLocation>()
            .ToList();

        Assert.DoesNotContain(owned, location => location.Name == "MaxTelemetryAllowed");
        Assert.DoesNotContain(owned, location => location.Name == "ShowedToastAtLevel");
        Assert.DoesNotContain(owned, location => location.Key.EndsWith(@"CurrentVersion\Policies\DataCollection"));
    }

    [Fact]
    public void Activity_history_sets_the_policy_for_the_machine_and_the_user()
    {
        var tweak = Find("privacy.activity-history");

        Assert.Equal(
            [RegistryHive.LocalMachine, RegistryHive.CurrentUser],
            tweak.Targets.Select(target => ((RegistryLocation)target.Location).Hive));
        Assert.All(tweak.Targets, target => Assert.Equal("PublishUserActivities", ((RegistryLocation)target.Location).Name));
    }

    // The preset is only honest if it writes exactly what the Tweaks it stands for would write, so
    // picking All off leaves every one of them reading Off rather than Custom.
    [Fact]
    public void The_suggestions_preset_writes_what_each_tweak_it_covers_writes()
    {
        var preset = Find("privacy.suggestions-preset");
        var covered = TweakCatalog.All
            .Where(tweak => tweak.Category == Category.Privacy && tweak.Id != preset.Id)
            .Where(tweak => tweak.Targets.All(target => preset.Targets.Contains(target)))
            .ToList();

        Assert.Equal(13, covered.Count);
        Assert.Equal(preset.Targets.Count, covered.Sum(tweak => tweak.Targets.Count));

        foreach (var optionId in new[] { "off", "on" })
        {
            var option = preset.Options.Single(candidate => candidate.Id == optionId);
            foreach (var tweak in covered)
            {
                var child = tweak.Options.Single(candidate => candidate.Id == optionId);
                Assert.All(tweak.Targets, target => Assert.Equal(child.Values[target], option.Values[target]));
            }
        }
    }

    [Fact]
    public void The_suggestions_preset_covers_the_lock_screen_and_task_view_as_winhances_switch_does()
    {
        var preset = Find("privacy.suggestions-preset");

        Assert.All(
            new[] { "privacy.windows-spotlight", "privacy.lock-screen-tips", "privacy.lock-screen-slideshow", "privacy.timeline-suggestions" },
            id => Assert.All(Find(id).Targets, target => Assert.Contains(target, preset.Targets)));

        // Not everything in the group is a ContentDeliveryManager value: these two have their own
        // Settings values and policies, and Winhance's switch leaves them alone too.
        Assert.All(
            new[] { "privacy.advertising-id", "privacy.tailored-experiences" },
            id => Assert.All(Find(id).Targets, target => Assert.DoesNotContain(target, preset.Targets)));
    }

    [Fact]
    public void No_tweak_writes_the_same_target_twice()
    {
        var preset = Find("privacy.suggestions-preset");

        Assert.Equal(preset.Targets.Count, preset.Targets.Distinct().Count());
        Assert.All(preset.Options, option => Assert.Equal(preset.Targets.Count, option.Values.Count));
    }
}
