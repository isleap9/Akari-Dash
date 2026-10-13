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
    [InlineData("update.policy", "without security fixes")]
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
    public void The_policy_row_is_the_only_one_that_shares_a_value_with_another_row_on_this_page()
    {
        // Two owners of one value fight: each reads the other's apply as drift of its own. The modes
        // of update.policy are the one place the catalog allows it, because covering the rows below is
        // half of what that row is for.
        var rows = Updates.Where(tweak => tweak.Id != "update.policy").ToList();

        Assert.Equal(
            rows.SelectMany(Locations).Count(),
            rows.SelectMany(Locations).Distinct().Count());
    }

    [Fact]
    public void The_policy_row_offers_winhances_four_modes_with_akarios_pause_among_them()
    {
        Assert.Equal(
            ["normal", "security-only", "akari-pause", "paused", "off"],
            Find("update.policy").Options.Select(option => option.Id));
    }

    [Fact]
    public void Security_updates_only_sets_the_three_rows_below_it_to_their_own_held_back_options()
    {
        // The preset relationship, as privacy.suggestions-preset has it: after this Option is applied,
        // each covered row reads its own state back correctly instead of reading Custom.
        var mode = OptionOf("update.policy", "security-only");
        (string Id, int Option)[] covered =
        [
            ("update.automatic-updates", 1), ("update.feature-updates", 1), ("update.quality-updates", 1),
        ];

        foreach (var (id, index) in covered)
        {
            var row = Find(id);
            var option = row.Options[index];
            Assert.All(row.Targets, target => Assert.Equal(option.Values[target], mode.Values[target]));
        }

        // And it writes nothing those three do not: no pause, no WSUS, no shutdown option.
        var theirs = covered.SelectMany(pair => Locations(Find(pair.Id))).ToHashSet();
        Assert.All(
            Find("update.policy").Targets.Where(target => !theirs.Contains((RegistryLocation)target.Location)),
            target => Assert.Null(mode.Values[target]));
    }

    [Fact]
    public void Pausing_for_a_long_time_also_hides_update_and_shut_down()
    {
        // The one other row Winhance's paused mode reaches into, so it must read itself back too.
        var shutdown = Find("update.shutdown-option");
        var hide = OptionOf("update.shutdown-option", "hide");
        var mode = OptionOf("update.policy", "paused");

        Assert.All(shutdown.Targets, target => Assert.Equal(hide.Values[target], mode.Values[target]));
    }

    [Fact]
    public void Akarios_pause_writes_the_six_values_settings_writes_and_nothing_else()
    {
        // AkariOS-Ultimate pauses with the Settings page's own six values; Winhance's mode writes
        // those plus the paused-status pair, FlightSettingsMaxPauseDays and the AU policies. So the
        // Windows Update page still works under the AkariOS Option and still reads Paused.
        var policy = Find("update.policy");
        var akari = OptionOf("update.policy", "akari-pause");
        string[] six =
        [
            "PauseUpdatesStartTime", "PauseFeatureUpdatesStartTime", "PauseQualityUpdatesStartTime",
            "PauseUpdatesExpiryTime", "PauseFeatureUpdatesEndTime", "PauseQualityUpdatesEndTime",
        ];

        var written = policy.Targets
            .Where(target => akari.Values[target] is not null)
            .Select(target => ((RegistryLocation)target.Location).Name)
            .ToList();
        Assert.Equal(six.Order(), written.Order());

        // Fixed dates, so the row reads back as an Option rather than for ever as Custom. The start
        // must be in the past and the end far enough out that the pause does not expire on its own.
        foreach (var target in policy.Targets.Where(target => akari.Values[target] is not null))
        {
            var written_ = Assert.IsType<RegistryValue>(akari.Values[target]);
            var when = DateTimeOffset.Parse((string)written_.Data);
            var isStart = ((RegistryLocation)target.Location).Name.Contains("Start");
            Assert.True(isStart ? when < DateTimeOffset.UtcNow : when > DateTimeOffset.UtcNow.AddYears(20), target.Location.ToString());
        }
    }

    [Fact]
    public void The_two_paused_modes_cannot_be_mistaken_for_one_another()
    {
        var policy = Find("update.policy");
        var akari = OptionOf("update.policy", "akari-pause");
        var winhance = OptionOf("update.policy", "paused");

        Assert.NotEqual(
            policy.Targets.Select(target => akari.Values[target]),
            policy.Targets.Select(target => winhance.Values[target]));

        // Winhance's is the superset: everything AkariOS writes, at the same value, plus its own.
        Assert.All(
            policy.Targets.Where(target => akari.Values[target] is not null),
            target => Assert.Equal(akari.Values[target], winhance.Values[target]));
    }

    [Fact]
    public void The_policy_row_is_the_only_owner_of_the_pause_window_and_the_wsus_values()
    {
        // They were left out as rows of their own: nobody picks AutoInstallMinorUpdates on purpose.
        string[] itsAlone =
        [
            "UseWUServer", "AutoInstallMinorUpdates", "AlwaysAutoRebootAtScheduledTime",
            "PauseUpdatesStartTime", "PauseUpdatesExpiryTime", "PauseFeatureUpdatesStartTime",
            "PauseFeatureUpdatesEndTime", "PauseQualityUpdatesStartTime", "PauseQualityUpdatesEndTime",
            "PausedFeatureDate", "PausedQualityDate", "PausedFeatureStatus", "PausedQualityStatus",
            "FlightSettingsMaxPauseDays",
        ];

        foreach (var name in itsAlone)
        {
            var owners = TweakCatalog.All
                .Where(tweak => tweak.Targets.Any(target => target.Location is RegistryLocation location && location.Name == name))
                .Select(tweak => tweak.Id);

            Assert.Equal("update.policy", Assert.Single(owners));
        }
    }

    [Fact]
    public void As_set_in_settings_writes_nothing_anywhere_it_is_offered()
    {
        // Deleting a policy value is what hands the choice back to the Windows Update page.
        var handsBack = Updates
            .SelectMany(tweak => tweak.Options.Select(option => (tweak, option)))
            .Where(pair => pair.option.Id is "settings-decides" or "windows-default" or "normal" or "show" or "include")
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

}
