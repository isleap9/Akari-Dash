using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class UpdateCatalogTests
{
    private const string AutoUpdatePolicies = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
    private const string WindowsUpdatePolicies = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
    private const string UpdateUxSettings = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

    private static readonly IReadOnlyList<DeclaredTweak> Updates =
        TweakCatalog.All.Where(tweak => tweak.Category == Category.Update).ToList();

    private static DeclaredTweak Find(string id) => Updates.Single(tweak => tweak.Id == id);

    private static TweakOption OptionOf(string id, string optionId) =>
        Find(id).Options.Single(option => option.Id == optionId);

    private static IEnumerable<RegistryLocation> Locations(DeclaredTweak tweak) =>
        tweak.Targets.Select(target => (RegistryLocation)target.Location);

    [Fact]
    public void The_update_page_holds_only_registry_tweaks_in_its_groups()
    {
        Assert.Equal(15, Updates.Count);
        Assert.All(Updates, tweak =>
        {
            Assert.StartsWith("update.", tweak.Id);
            Assert.All(tweak.Targets, target => Assert.IsType<RegistryLocation>(target.Location));
        });
        Assert.Equal(
            ["Update policy", "Update behaviour", "Update content", "Delivery & Store"],
            Updates.Select(tweak => tweak.Group).Distinct());
    }

    [Fact]
    public void Only_delivery_optimization_is_recommended_so_apply_all_never_stops_security_fixes()
    {
        // Every other row here changes whether or when updates arrive, which is not a performance
        // call: "apply all recommended" must not be how a PC stops being patched.
        var recommended = Assert.Single(Updates, tweak => tweak.Recommended is not null);

        Assert.Equal("update.delivery-optimization", recommended.Id);
        Assert.Equal("off", recommended.Recommended?.Id);
    }

    [Theory]
    [InlineData("update.automatic-updates", "without security updates")]
    [InlineData("update.feature-updates", "Windows Home ignores it")]
    [InlineData("update.quality-updates", "unpatched")]
    [InlineData("update.restart-while-signed-in", "not yet in force")]
    [InlineData("update.update-notifications", "with nothing said beforehand")]
    [InlineData("update.restart-notification", "nothing is said")]
    [InlineData("update.drivers", "generic driver")]
    [InlineData("update.store-app-updates", "security fixes included")]
    [InlineData("update.policy", "arrive late")]
    public void A_row_that_holds_an_update_back_says_so_in_its_own_description(string id, string cost)
    {
        // The rule for this page, from the ticket: a user who defers an update or blocks a driver
        // should know it from the description, not find out months later.
        Assert.Contains(cost, Find(id).Description);
    }

    [Fact]
    public void Nothing_else_in_the_catalog_writes_a_value_this_page_owns()
    {
        var mine = Updates.SelectMany(Locations).ToHashSet();
        var others = TweakCatalog.All
            .Where(tweak => tweak.Category != Category.Update)
            .SelectMany(tweak => tweak.Targets.Select(target => (tweak.Id, target.Location)))
            .ToList();

        Assert.DoesNotContain(others, other => mine.Contains(other.Location));
    }

    [Fact]
    public void The_preset_is_the_only_row_that_shares_a_value_with_another_row_on_this_page()
    {
        // Two owners of one value fight: each reads the other's apply as drift of its own. The preset
        // is the one place the catalog allows it, because covering the rows below is its whole job.
        var rows = Updates.Where(tweak => tweak.Id != "update.policy").ToList();

        Assert.Equal(
            rows.SelectMany(Locations).Count(),
            rows.SelectMany(Locations).Distinct().Count());
    }

    [Fact]
    public void The_preset_covers_exactly_the_three_update_policy_rows_it_names()
    {
        var preset = Find("update.policy");
        string[] covered = ["update.automatic-updates", "update.feature-updates", "update.quality-updates"];

        Assert.Equal(
            covered.SelectMany(id => Locations(Find(id))).ToHashSet(),
            Locations(preset).ToHashSet());

        // Each Option writes what the covered row's own Option of the same mind writes, value for value.
        foreach (var (presetOption, rowOption) in new[] { ("settings-decides", 0), ("hold-back", 1) })
        {
            foreach (var id in covered)
            {
                var row = Find(id);
                var option = row.Options[rowOption];
                Assert.All(row.Targets, target =>
                    Assert.Equal(option.Values[target], OptionOf("update.policy", presetOption).Values[target]));
            }
        }
    }

    [Fact]
    public void As_set_in_settings_writes_nothing_anywhere_it_is_offered()
    {
        // Deleting a policy value is what hands the choice back to the Windows Update page.
        var handsBack = Updates
            .SelectMany(tweak => tweak.Options.Select(option => (tweak, option)))
            .Where(pair => pair.option.Id is "settings-decides" or "windows-default" or "show" or "include")
            .ToList();

        Assert.Equal(9, handsBack.Count);
        Assert.All(handsBack, pair => Assert.All(pair.tweak.Targets, target => Assert.Null(pair.option.Values[target])));
    }

    [Fact]
    public void Automatic_updates_offers_the_three_policy_levels_and_off()
    {
        var tweak = Find("update.automatic-updates");

        Assert.Equal(
            ["settings-decides", "notify-before-download", "download-then-ask", "off"],
            tweak.Options.Select(option => option.Id));

        // AUOptions is only read while NoAutoUpdate is absent or 0, so only "off" writes NoAutoUpdate.
        var noAutoUpdate = tweak.Targets.Where(target => ((RegistryLocation)target.Location).Name == "NoAutoUpdate").ToList();
        Assert.All(tweak.Options.Where(option => option.Id != "off"),
            option => Assert.All(noAutoUpdate, target => Assert.Null(option.Values[target])));
        Assert.All(noAutoUpdate,
            target => Assert.Equal(RegistryValue.DWord(1), OptionOf("update.automatic-updates", "off").Values[target]));

        var auOptions = tweak.Targets.Where(target => ((RegistryLocation)target.Location).Name == "AUOptions").ToList();
        Assert.All(auOptions, target => Assert.Equal(RegistryValue.DWord(2), OptionOf("update.automatic-updates", "notify-before-download").Values[target]));
        Assert.All(auOptions, target => Assert.Equal(RegistryValue.DWord(3), OptionOf("update.automatic-updates", "download-then-ask").Values[target]));
        Assert.All(auOptions, target => Assert.Equal(RegistryValue.DWord(1), OptionOf("update.automatic-updates", "off").Values[target]));
    }

    [Theory]
    [InlineData("update.automatic-updates", "NoAutoUpdate")]
    [InlineData("update.automatic-updates", "AUOptions")]
    [InlineData("update.restart-while-signed-in", "NoAutoRebootWithLoggedOnUsers")]
    [InlineData("update.shutdown-option", "NoAUShutdownOption")]
    public void An_automatic_updates_policy_is_written_for_the_machine_and_for_the_user(string id, string name)
    {
        // Winhance writes both copies of every AU value; the Windows Update Agent reads the machine one.
        var pair = Locations(Find(id)).Where(location => location.Name == name).ToList();

        Assert.Equal([RegistryHive.LocalMachine, RegistryHive.CurrentUser], pair.Select(location => location.Hive));
        Assert.All(pair, location => Assert.Equal(AutoUpdatePolicies, location.Key));
    }

    [Fact]
    public void Deferral_writes_the_documented_policy_key_not_the_page_windows_11_dropped()
    {
        // Winhance writes these names under HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings, which
        // is where Windows 10's "choose when updates are installed" page kept them. That page is gone.
        var feature = Find("update.feature-updates");
        var quality = Find("update.quality-updates");

        Assert.All(Locations(feature).Concat(Locations(quality)), location =>
        {
            Assert.Equal(RegistryHive.LocalMachine, location.Hive);
            Assert.Equal(WindowsUpdatePolicies, location.Key);
        });

        Assert.Equal(
            ["BranchReadinessLevel", "DeferFeatureUpdates", "DeferFeatureUpdatesPeriodInDays"],
            Locations(feature).Select(location => location.Name));
        Assert.Equal(
            [RegistryValue.DWord(20), RegistryValue.DWord(1), RegistryValue.DWord(365)],
            feature.Targets.Select(target => OptionOf("update.feature-updates", "hold-back-a-year").Values[target]));

        Assert.Equal(
            ["DeferQualityUpdates", "DeferQualityUpdatesPeriodInDays"],
            Locations(quality).Select(location => location.Name));
        Assert.Equal(
            [RegistryValue.DWord(1), RegistryValue.DWord(7)],
            quality.Targets.Select(target => OptionOf("update.quality-updates", "hold-back-a-week").Values[target]));
    }

    [Fact]
    public void Update_notifications_offers_the_documented_levels_rather_than_winhances_toggle()
    {
        // SetUpdateNotificationLevel: 1 keeps the restart warnings, 2 drops those too. Winhance maps
        // its on/off toggle to 2/1, which cannot be right for a switch that shows or hides them.
        var tweak = Find("update.update-notifications");

        Assert.Equal(["settings-decides", "restart-warnings-only", "none"], tweak.Options.Select(option => option.Id));
        Assert.All(tweak.Targets, target =>
        {
            Assert.Equal(RegistryValue.DWord(1), OptionOf(tweak.Id, "restart-warnings-only").Values[target]);
            Assert.Equal(RegistryValue.DWord(2), OptionOf(tweak.Id, "none").Values[target]);
            Assert.Equal("SetUpdateNotificationLevel", ((RegistryLocation)target.Location).Name);
        });
    }

    [Theory]
    [InlineData("update.latest-updates", "IsContinuousInnovationOptedIn")]
    [InlineData("update.other-microsoft-products", "AllowMUUpdateService")]
    [InlineData("update.restart-asap", "IsExpedited")]
    [InlineData("update.restart-notification", "RestartNotificationsAllowed2")]
    [InlineData("update.metered-connection", "AllowAutoWindowsUpdateDownloadOverMeteredNetwork")]
    public void A_windows_update_page_switch_is_off_where_its_value_is_missing(string id, string name)
    {
        var tweak = Find(id);
        var target = Assert.Single(tweak.Targets);
        var location = (RegistryLocation)target.Location;

        Assert.Equal(RegistryHive.LocalMachine, location.Hive);
        Assert.Equal(UpdateUxSettings, location.Key);
        Assert.Equal(name, location.Name);

        // None of these exists until the switch is used, and Windows reads a missing one as off, so a
        // fresh PC reads as Off rather than as Custom.
        Assert.Equal(["on", "off"], tweak.Options.Select(option => option.Id));
        Assert.Equal(RegistryValue.DWord(0), target.AbsentMeans);
        Assert.Equal(target.AbsentMeans, OptionOf(id, "off").Values[target]);
    }

    [Fact]
    public void Driver_updates_are_left_out_by_the_policy_for_the_machine_and_the_user()
    {
        var tweak = Find("update.drivers");

        Assert.Equal(["include", "exclude"], tweak.Options.Select(option => option.Id));
        Assert.All(Locations(tweak), location =>
        {
            Assert.Equal(WindowsUpdatePolicies, location.Key);
            Assert.Equal("ExcludeWUDriversInQualityUpdate", location.Name);
        });
        Assert.All(tweak.Targets, target => Assert.Equal(RegistryValue.DWord(1), OptionOf(tweak.Id, "exclude").Values[target]));
    }

    [Fact]
    public void Delivery_optimization_offers_winhances_four_modes()
    {
        var tweak = Find("update.delivery-optimization");

        Assert.Equal(["settings-decides", "this-network", "network-and-internet", "off"], tweak.Options.Select(option => option.Id));
        Assert.All(tweak.Targets, target =>
        {
            Assert.Equal("DODownloadMode", ((RegistryLocation)target.Location).Name);
            Assert.Equal(RegistryValue.DWord(1), OptionOf(tweak.Id, "this-network").Values[target]);
            Assert.Equal(RegistryValue.DWord(3), OptionOf(tweak.Id, "network-and-internet").Values[target]);
            Assert.Equal(RegistryValue.DWord(99), OptionOf(tweak.Id, "off").Values[target]);
        });
    }

    [Fact]
    public void Store_app_updates_read_as_on_where_the_policy_is_missing()
    {
        // AutoDownload: 4 updates apps by itself, 2 never does, and a missing value behaves as 4.
        var tweak = Find("update.store-app-updates");

        Assert.All(tweak.Targets, target =>
        {
            Assert.Equal(@"SOFTWARE\Policies\Microsoft\WindowsStore", ((RegistryLocation)target.Location).Key);
            Assert.Equal(RegistryValue.DWord(4), target.AbsentMeans);
            Assert.Equal(RegistryValue.DWord(4), OptionOf(tweak.Id, "on").Values[target]);
            Assert.Equal(RegistryValue.DWord(2), OptionOf(tweak.Id, "off").Values[target]);
        });
    }

    [Fact]
    public void Nothing_here_claims_to_need_a_sign_out_or_a_restart()
    {
        // These are the values Windows Update reads at its next scan; no part of Windows has to be
        // restarted for one to take, and the read-back is the value itself.
        Assert.All(Updates, tweak => Assert.Equal(Activation.Immediately, tweak.Activation));
    }

    [Fact]
    public void Winhances_wsus_and_paused_mode_values_are_left_alone()
    {
        // Documented in TweakCatalog.Update.cs: UseWUServer is not this page's decision, and the
        // paused mode writes a pause window with dates baked into the build.
        string[] untouched =
        [
            "UseWUServer", "AutoInstallMinorUpdates", "AlwaysAutoRebootAtScheduledTime",
            "PauseUpdatesStartTime", "PauseUpdatesExpiryTime", "PauseFeatureUpdatesStartTime",
            "PauseFeatureUpdatesEndTime", "PauseQualityUpdatesStartTime", "PauseQualityUpdatesEndTime",
            "PausedFeatureDate", "PausedQualityDate", "PausedFeatureStatus", "PausedQualityStatus",
            "FlightSettingsMaxPauseDays",
        ];

        Assert.DoesNotContain(
            TweakCatalog.All.SelectMany(tweak => tweak.Targets).Select(target => target.Location).OfType<RegistryLocation>(),
            location => untouched.Contains(location.Name));
    }
}
