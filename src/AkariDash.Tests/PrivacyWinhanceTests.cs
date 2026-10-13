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
        var tweak = Find("security.uac-level");
        var option = tweak.Options.Single(candidate => candidate.Id == optionId);

        Assert.Equal(RegistryValue.DWord(behavior), option.Values[tweak.Targets[0]]);
        Assert.Equal(RegistryValue.DWord(secureDesktop), option.Values[tweak.Targets[1]]);
    }

    [Fact]
    public void Uac_counts_a_machine_with_no_values_as_the_level_windows_ships_with()
    {
        var tweak = Find("security.uac-level");

        Assert.Equal(RegistryValue.DWord(5), tweak.Targets[0].AbsentMeans);
        Assert.Equal(RegistryValue.DWord(1), tweak.Targets[1].AbsentMeans);
        Assert.Contains(tweak.Options, option => option.Id == "apps");
    }

    [Theory]
    [InlineData("security.automatic-device-encryption", 0)]
    [InlineData("security.remote-assistance", 1)]
    [InlineData("security.wifi-sense", 0)]
    public void The_security_group_tweaks_count_a_missing_value_as_what_windows_does(string id, int absent)
    {
        Assert.All(Find(id).Targets, target => Assert.Equal(RegistryValue.DWord(absent), target.AbsentMeans));
    }

    [Fact]
    public void Wifi_sense_switches_both_of_its_policy_manager_values()
    {
        var tweak = Find("security.wifi-sense");

        Assert.Equal(
            [@"SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting",
             @"SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots"],
            tweak.Targets.Select(target => ((RegistryLocation)target.Location).Key));
        Assert.All(tweak.Targets, target => Assert.Equal("Value", ((RegistryLocation)target.Location).Name));
    }
}
