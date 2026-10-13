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
    [InlineData("update.delivery-optimization", "leaves it that way after an Undo")]
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
    public void No_two_rows_on_this_page_share_a_value()
    {
        // Two owners of one value fight: each reads the other's apply as drift of its own. The old
        // update.policy modes shared values with the rows below on purpose, and that is where this
        // page's Drift and Original Values bugs came from.
        Assert.Equal(
            Updates.SelectMany(Locations).Count(),
            Updates.SelectMany(Locations).Distinct().Count());
    }

    [Fact]
    public void The_policy_row_offers_normal_and_akarios_pause_only()
    {
        // Winhance's Security updates only and Disabled were presets over rows below that still
        // exist, and its own pause wore most of Disabled. AkariOS-Ultimate's pause is the one kept.
        Assert.Equal(
            [("normal", "Normal (Windows default)"), ("akari-pause", "Paused")],
            Find("update.policy").Options.Select(option => (option.Id, option.Label)));
    }

    [Fact]
    public void Akarios_pause_writes_the_six_values_settings_writes_and_nothing_else()
    {
        // The Settings page's own six values and nothing else, so the Windows Update page keeps
        // working under this Option and still reads Paused, with a Resume button.
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
    public void The_paused_option_names_the_year_settings_will_show()
    {
        // Confirmed on the VM: Settings reads "Updates paused" and names the end date this Option
        // writes. Someone who opens that page and finds 2051 should have read it here first, so the
        // date and the description are not allowed to drift apart.
        var policy = Find("update.policy");
        var paused = OptionOf("update.policy", "akari-pause");
        var ends = policy.Targets
            .Select(target => paused.Values[target])
            .OfType<RegistryValue>()
            .Select(value => DateTimeOffset.Parse((string)value.Data))
            .Max();

        Assert.Contains(ends.Year.ToString(), policy.Description);
    }

    [Fact]
    public void The_policy_row_is_the_only_owner_of_the_pause_window()
    {
        string[] itsAlone =
        [
            "PauseUpdatesStartTime", "PauseUpdatesExpiryTime", "PauseFeatureUpdatesStartTime",
            "PauseFeatureUpdatesEndTime", "PauseQualityUpdatesStartTime", "PauseQualityUpdatesEndTime",
        ];

        foreach (var name in itsAlone)
        {
            Assert.Equal("update.policy", Assert.Single(Owners(name)));
        }
    }

    [Fact]
    public void Nothing_writes_what_only_winhances_paused_mode_wrote()
    {
        // Dropped with Winhance's paused mode, and UseWUServer with its Disabled. AutoInstallMinorUpdates and AlwaysAutoRebootAtScheduledTime are
        // not decisions anyone makes one at a time, and the rest are Windows Update's own bookkeeping
        // about a pause, which the six dates above are enough to ask for.
        string[] gone =
        [
            "AutoInstallMinorUpdates", "AlwaysAutoRebootAtScheduledTime",
            "PausedFeatureDate", "PausedQualityDate", "PausedFeatureStatus", "PausedQualityStatus",
            "FlightSettingsMaxPauseDays", "UseWUServer",
        ];

        Assert.All(gone, name => Assert.Empty(Owners(name)));
    }

    private static IEnumerable<string> Owners(string valueName) => TweakCatalog.All
        .Where(tweak => tweak.Targets.Any(target => target.Location is RegistryLocation location && location.Name == valueName))
        .Select(tweak => tweak.Id);

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

    [Fact]
    public void The_pause_and_the_rows_below_it_each_read_their_own_state()
    {
        // Reported from the VM build of #31, when the policy modes shared values with the rows below:
        // Paused, then Automatic updates > Never check on its own, and the policy row claimed Drift.
        // The pause now owns its six values alone, so each row reads exactly the Option it was given.
        var engine = new TweakEngine(new InMemoryMachine());
        var policy = Find("update.policy");
        var automatic = Find("update.automatic-updates");

        engine.Apply(policy, OptionOf("update.policy", "akari-pause"));
        engine.Apply(automatic, OptionOf("update.automatic-updates", "off"));

        Assert.Same(
            OptionOf("update.policy", "akari-pause"),
            Assert.IsType<LiveState.InOption>(engine.ReadLiveState(policy, TweakCatalog.All)).Option);
        Assert.Same(
            OptionOf("update.automatic-updates", "off"),
            Assert.IsType<LiveState.InOption>(engine.ReadLiveState(automatic, TweakCatalog.All)).Option);
    }

    [Fact]
    public void A_policy_mode_that_windows_moved_still_reads_as_drift()
    {
        var machine = new InMemoryMachine();
        var engine = new TweakEngine(machine);
        var policy = Find("update.policy");

        engine.Apply(policy, OptionOf("update.policy", "akari-pause"));

        // Nothing on this page asks for an empty pause window, so this one is Windows' doing.
        machine.Write(
            policy.Targets.Single(target => ((RegistryLocation)target.Location).Name == "PauseUpdatesExpiryTime").Location,
            RegistryValue.String("2026-11-01T00:00:00Z"));

        Assert.IsType<LiveState.Drifted>(engine.ReadLiveState(policy, TweakCatalog.All));
    }

    [Fact]
    public void Undoing_the_pause_ends_it()
    {
        var machine = new InMemoryMachine();
        var engine = new TweakEngine(machine);
        var policy = Find("update.policy");

        engine.Apply(policy, OptionOf("update.policy", "akari-pause"));
        engine.Undo(policy);

        Assert.All(policy.Targets, target => Assert.Null(machine.Read(target.Location)));
    }
}
