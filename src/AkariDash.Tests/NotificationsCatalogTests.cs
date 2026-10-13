using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class NotificationsCatalogTests
{
    private static readonly IReadOnlyList<DeclaredTweak> Notifications =
        TweakCatalog.All.Where(tweak => tweak.Category == Category.Notifications).ToList();

    private static DeclaredTweak Find(string id) => Notifications.Single(tweak => tweak.Id == id);

    private static RegistryLocation Registry(RegistryHive hive, string key, string name) => new(hive, key, name);

    [Fact]
    public void Notifications_page_holds_only_registry_tweaks_in_its_groups()
    {
        Assert.Equal(15, Notifications.Count);
        Assert.All(Notifications, tweak =>
        {
            Assert.StartsWith("notifications.", tweak.Id);
            Assert.All(tweak.Targets, target => Assert.IsType<RegistryLocation>(target.Location));
        });
        Assert.Equal(
            ["Notifications", "Suggestions", "System notifications", "Windows Security"],
            Notifications.Select(tweak => tweak.Group).Distinct());
    }

    [Fact]
    public void Being_told_about_things_is_a_matter_of_taste_so_none_has_a_recommended_option()
    {
        Assert.All(Notifications, tweak => Assert.Null(tweak.Recommended));
    }

    [Fact]
    public void Every_tweak_is_a_plain_on_off_pair()
    {
        Assert.All(Notifications, tweak => Assert.Equal(["on", "off"], tweak.Options.Select(option => option.Id)));
    }

    [Fact]
    public void Missing_values_read_as_on_rather_than_custom()
    {
        // A fresh Windows has most of these missing, and shows every one of these notifications.
        Assert.All(Notifications, tweak =>
        {
            var on = tweak.Options.Single(option => option.Id == "on");
            Assert.All(tweak.Targets, target => Assert.Equal(on.Values[target], target.AbsentMeans));
        });
    }

    [Fact]
    public void Nothing_else_in_the_catalog_writes_a_value_this_page_owns()
    {
        // Two owners of one value would fight: each reads the other's apply as drift of its own. The
        // catalog allows it only where a preset Tweak deliberately covers the rows beside it, and
        // nothing on this page is a preset.
        var mine = Notifications.SelectMany(tweak => tweak.Targets).Select(target => target.Location).ToHashSet();
        var others = TweakCatalog.All
            .Where(tweak => tweak.Category != Category.Notifications)
            .SelectMany(tweak => tweak.Targets.Select(target => (tweak.Id, target.Location)))
            .ToList();

        Assert.DoesNotContain(others, other => mine.Contains(other.Location));
    }

    [Fact]
    public void The_master_switch_waits_for_a_sign_out_because_nothing_restarts_the_notification_service()
    {
        var tweak = Find("notifications.notifications");

        Assert.Equal(Activation.AfterSignOut, tweak.Activation);
        Assert.Equal(
            Registry(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled"),
            Assert.Single(tweak.Targets).Location);
    }

    [Fact]
    public void Lock_screen_notifications_write_both_the_platform_and_the_settings_value()
    {
        var tweak = Find("notifications.lock-screen");

        Assert.Equal(
            ["NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK", "LockScreenToastEnabled"],
            tweak.Targets.Select(target => ((RegistryLocation)target.Location).Name));
        Assert.All(tweak.Targets, target => Assert.Equal(RegistryValue.DWord(0), tweak.Options.Single(option => option.Id == "off").Values[target]));
    }

    [Theory]
    [InlineData("notifications.app-permissions", "CapabilityAccess")]
    [InlineData("notifications.startup-apps", "StartupApp")]
    [InlineData("notifications.security-and-maintenance", "SecurityAndMaintenance")]
    public void A_windows_sender_is_switched_off_under_its_own_name(string id, string sender)
    {
        var location = (RegistryLocation)Assert.Single(Find(id).Targets).Location;

        Assert.Equal($@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.{sender}", location.Key);
        Assert.Equal("Enabled", location.Name);
    }

    [Theory]
    [InlineData("notifications.tips", "SubscribedContent-338389Enabled")]
    [InlineData("notifications.welcome-experience", "SubscribedContent-310093Enabled")]
    [InlineData("notifications.finish-setup", "ScoobeSystemSettingEnabled")]
    public void The_three_tweaks_that_moved_off_the_windows_page_still_write_the_same_value(string id, string name)
    {
        // Their ids moved with them, so Home offers an Undo for a PC that applied the windows. one.
        Assert.Equal(name, ((RegistryLocation)Assert.Single(Find(id).Targets).Location).Name);
        Assert.DoesNotContain(TweakCatalog.All.Where(tweak => tweak.Category == Category.Windows), tweak => tweak.Group == "Notifications");
    }

    [Fact]
    public void Silencing_windows_security_writes_the_machine_value_and_both_policy_copies()
    {
        var tweak = Find("notifications.windows-security");
        var off = tweak.Options.Single(option => option.Id == "off");

        Assert.Equal(5, tweak.Targets.Count);
        Assert.All(tweak.Targets, target =>
        {
            // DisableNotifications, so Off writes 1, and a missing value is the notifications-on state.
            Assert.Equal(RegistryValue.DWord(1), off.Values[target]);
            Assert.Equal(RegistryValue.DWord(0), target.AbsentMeans);
            Assert.StartsWith("DisableNotifications", ((RegistryLocation)target.Location).Name.Replace("DisableEnhanced", "Disable"));
        });
        Assert.Equal(4, tweak.Targets.Count(target => ((RegistryLocation)target.Location).Key.Contains(@"Policies\Microsoft\Windows Defender Security Center")));
    }

    [Fact]
    public void The_security_pages_own_notification_tweaks_are_left_alone()
    {
        // Two Tweaks over Windows Security notifications: this one silences everything, while the
        // Security page keeps the alerts and drops the routine messages. Neither writes the other's values.
        var mine = Find("notifications.windows-security").Targets.Select(target => target.Location);
        var security = TweakCatalog.All.Single(tweak => tweak.Id == "security.security-notifications").Targets.Select(target => target.Location);

        Assert.Empty(mine.Intersect(security));
    }
}
