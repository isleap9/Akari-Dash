using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

/// <summary>AkariOS-Ultimate's Advanced 'Defender' Tweak (Tweaks/Advanced.ps1:3-435): one row that
/// turns Defender off and on, written in safe mode like Defender optimize.</summary>
public class DefenderOffTests
{
    private static DeclaredTweak Tweak => TweakCatalog.All.Single(tweak => tweak.Id == "security.disable-defender");
    private static TweakOption Option(string id) => Tweak.Options.Single(option => option.Id == id);

    private static MachineValue? At(string key, string name) => At(new RegistryLocation(RegistryHive.LocalMachine, key, name));
    private static MachineValue? At(MachineLocation location) => Option(CurrentOption).Values[Tweak.Targets.Single(target => target.Location == location)];
    private static string CurrentOption = "off";

    private static RegistryValue Binary(string hex) => new(RegistryValueKind.Binary, Convert.FromHexString(hex));

    // The eleven services and drivers, with the Start value Windows ships them at, which is this
    // Tweak's On. Off is 4 (Disabled) for every one of them.
    private static readonly (string Name, int On)[] Services =
    [
        ("WdNisSvc", 3), ("WinDefend", 2), ("MDCoreSvc", 2), ("wscsvc", 2),
        ("webthreatdefsvc", 3), ("webthreatdefusersvc", 2), ("Sense", 3), ("SecurityHealthService", 2),
        ("WdBoot", 0), ("WdFilter", 0), ("WdNisDrv", 3),
    ];

    private static IReadOnlyList<TweakTarget> Tasks =>
        [.. Tweak.Targets.Where(target => target.Location is ScheduledTaskLocation)];

    private static TweakTarget Service(string name) =>
        Tweak.Targets.Single(target => target.Location == new RegistryLocation(RegistryHive.LocalMachine, $@"SYSTEM\CurrentControlSet\Services\{name}", "Start"));

    // A pass target has to exist before the Tweak can be applied, so the fake machine holds Defender's
    // own tasks, as a PC would.
    private static InMemoryMachine DefenderMachine() =>
        new InMemoryMachine()
            .With(new ScheduledTaskLocation(@"\Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance"), TaskEnabledValue.On)
            .With(new ScheduledTaskLocation(@"\Microsoft\Windows\Windows Defender\Windows Defender Cleanup"), TaskEnabledValue.On)
            .With(new ScheduledTaskLocation(@"\Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan"), TaskEnabledValue.On)
            .With(new ScheduledTaskLocation(@"\Microsoft\Windows\Windows Defender\Windows Defender Verification"), TaskEnabledValue.On);

    [Fact]
    public void The_row_is_the_disable_defender_row_of_the_security_page()
    {
        Assert.Equal("Disable Defender", Tweak.Title);
        Assert.Equal(Category.Security, Tweak.Category);
        Assert.Equal("Microsoft Defender", Tweak.Group);
        Assert.Equal(Activation.InSafeMode, Tweak.Activation);
        Assert.Null(Tweak.Recommended);
        Assert.Equal(["off", "on"], Tweak.Options.Select(option => option.Id));
        Assert.Equal(["Off", "On"], Tweak.Options.Select(option => option.Label));
    }

    // AkariOS's disable list, value for value (Tweaks/Advanced.ps1:40-159).
    [Fact]
    public void Off_writes_what_akarios_advanced_defender_writes()
    {
        // Virus & threat protection: real-time and dev drive protection off, cloud protection and
        // sample submission off, tamper protection off, Controlled Folder Access off.
        Assert.Equal(RegistryValue.DWord(1), At(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring"));
        Assert.Equal(RegistryValue.DWord(1), At(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableAsyncScanOnOpen"));
        Assert.Equal(RegistryValue.DWord(0), At(@"SOFTWARE\Microsoft\Windows Defender\Spynet", "SpyNetReporting"));
        Assert.Equal(RegistryValue.DWord(0), At(@"SOFTWARE\Microsoft\Windows Defender\Spynet", "SubmitSamplesConsent"));
        Assert.Equal(RegistryValue.DWord(4), At(@"SOFTWARE\Microsoft\Windows Defender\Features", "TamperProtection"));
        Assert.Equal(RegistryValue.DWord(0), At(@"SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access", "EnableControlledFolderAccess"));

        // App & browser control: unwanted app blocking, trusted mode, Smart Locker mode and Smart App
        // Control, which Windows marks off for good.
        Assert.Equal(RegistryValue.DWord(0), At(@"SOFTWARE\Microsoft\Windows Defender", "PUAProtection"));
        Assert.Equal(RegistryValue.DWord(0), At(@"SOFTWARE\Microsoft\Windows Defender", "VerifiedAndReputableTrustModeEnabled"));
        Assert.Equal(RegistryValue.DWord(0), At(@"SOFTWARE\Microsoft\Windows Defender", "SmartLockerMode"));
        Assert.Equal(RegistryValue.DWord(0), At(@"SYSTEM\CurrentControlSet\Control\AppID\Configuration\SMARTLOCKER", "START_PENDING"));
        Assert.Equal(Binary("0000000000000000"), At(@"SYSTEM\CurrentControlSet\Control\AppID\Configuration\SMARTLOCKER", "ENABLED"));
        Assert.Equal(RegistryValue.DWord(0), At(@"SYSTEM\CurrentControlSet\Control\CI\Policy", "VerifiedAndReputablePolicyState"));

        // Windows Security's own notifications, the firewall's, and SmartScreen.
        Assert.Equal(RegistryValue.DWord(1), At(@"SOFTWARE\Microsoft\Windows Defender Security Center\Notifications", "DisableEnhancedNotifications"));
        Assert.Equal(RegistryValue.DWord(1), At(@"SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection", "NoActionNotificationDisabled"));
        Assert.Equal(RegistryValue.DWord(1), At(@"SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection", "SummaryNotificationDisabled"));
        Assert.Equal(RegistryValue.DWord(1), At(@"SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection", "FilesBlockedNotificationDisabled"));
        Assert.Equal(RegistryValue.DWord(1231), At(@"SYSTEM\ControlSet001\Services\SharedAccess\Epoch", "Epoch"));
        Assert.Equal(RegistryValue.DWord(1), At(@"SYSTEM\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile", "DisableNotifications"));
        Assert.Equal(RegistryValue.DWord(1), At(@"SYSTEM\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile", "DisableNotifications"));
        Assert.Equal(RegistryValue.DWord(1), At(@"SYSTEM\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile", "DisableNotifications"));
        Assert.Equal(RegistryValue.String("Off"), At(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled"));

        // Exploit protection's mask at Advanced.ps1:104, which is not the optimize Tweak's mask.
        Assert.Equal(Binary("222222000001000000000000000000000000000000000000"), At(@"SYSTEM\ControlSet001\Control\Session Manager\kernel", "MitigationOptions"));

        // Memory integrity off with its bookkeeping deleted, VBS deleted with its hypervisor boot
        // settings, LSA protection off and the vulnerable driver blocklist off.
        Assert.Equal(RegistryValue.DWord(0), At(@"SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled"));
        Assert.Null(At(@"SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "WasEnabledBy"));
        Assert.Null(At(@"SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "ChangedInBootCycle"));
        Assert.Null(At(@"SYSTEM\ControlSet001\Control\DeviceGuard", "EnableVirtualizationBasedSecurity"));
        Assert.Null(At(new BootSettingLocation("hypervisorlaunchtype")));
        Assert.Null(At(new BootSettingLocation("isolatedcontext")));
        Assert.Null(At(new BootSettingLocation("allowedinmemorysettings")));
        Assert.Equal(RegistryValue.DWord(0), At(@"SYSTEM\CurrentControlSet\Control\Lsa", "RunAsPPL"));
        Assert.Equal(RegistryValue.DWord(0), At(@"SYSTEM\ControlSet001\Control\CI\Config", "VulnerableDriverBlocklistEnable"));

        // The account protection reminders and Edge's and Store apps' SmartScreen, off.
        Assert.Equal(RegistryValue.DWord(1), At(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection", "DisableNotifications")));
        Assert.Equal(RegistryValue.DWord(1), At(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection", "DisableDynamiclockNotifications")));
        Assert.Equal(RegistryValue.DWord(1), At(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection", "DisableWindowsHelloNotifications")));
        Assert.Equal(RegistryValue.DWord(0), At(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenEnabled", "")));
        Assert.Equal(RegistryValue.DWord(0), At(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenPuaEnabled", "")));
        Assert.Equal(RegistryValue.DWord(0), At(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost", "EnableWebContentEvaluation")));

        // The four Defender tasks and the eleven services and drivers, all disabled.
        Assert.All(Tasks, task => Assert.Equal(TaskEnabledValue.Off, Option("off").Values[task]));
        Assert.All(Services, service => Assert.Equal(RegistryValue.DWord(4), Option("off").Values[Service(service.Name)]));
    }

    // A service is written as its registry Start value, not as a ServiceLocation: the registry is the
    // only write that reaches all eleven, and a missing Start counts as what Windows ships (the On
    // value), so an edition without the service reads On rather than Custom.
    [Fact]
    public void The_services_and_drivers_are_registry_start_values_at_their_windows_default_when_absent()
    {
        Assert.Equal(11, Services.Length);

        foreach (var (name, on) in Services)
        {
            var target = Service(name);

            Assert.Equal(RegistryValue.DWord(on), target.AbsentMeans);
            Assert.False(target.WrittenInOwnSession);
        }

        Assert.DoesNotContain(Tweak.Targets, target => target.Location is ServiceLocation);
    }

    // AkariOS's enable list (Tweaks/Advanced.ps1:259-370), value for value.
    [Fact]
    public void On_writes_what_akarios_enable_list_writes()
    {
        // The option helper is a mutable static, so the test puts it back in a finally block: an
        // assert that throws must not leave every later test in the class reading the On values.
        CurrentOption = "on";
        try
        {
            Assert.Equal(RegistryValue.DWord(0), At(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring"));
            Assert.Equal(RegistryValue.DWord(0), At(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableAsyncScanOnOpen"));
            Assert.Equal(RegistryValue.DWord(2), At(@"SOFTWARE\Microsoft\Windows Defender\Spynet", "SpyNetReporting"));
            Assert.Equal(RegistryValue.DWord(1), At(@"SOFTWARE\Microsoft\Windows Defender\Spynet", "SubmitSamplesConsent"));
            Assert.Equal(RegistryValue.DWord(5), At(@"SOFTWARE\Microsoft\Windows Defender\Features", "TamperProtection"));
            Assert.Equal(RegistryValue.DWord(1), At(@"SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access", "EnableControlledFolderAccess"));

            // The enable list writes PUAProtection 2 and then 1 again; 1 is the value it settles on.
            Assert.Equal(RegistryValue.DWord(1), At(@"SOFTWARE\Microsoft\Windows Defender", "PUAProtection"));
            Assert.Equal(RegistryValue.DWord(1), At(@"SOFTWARE\Microsoft\Windows Defender", "VerifiedAndReputableTrustModeEnabled"));
            Assert.Equal(RegistryValue.DWord(1), At(@"SOFTWARE\Microsoft\Windows Defender", "SmartLockerMode"));
            Assert.Equal(RegistryValue.DWord(4), At(@"SYSTEM\CurrentControlSet\Control\AppID\Configuration\SMARTLOCKER", "START_PENDING"));
            Assert.Equal(Binary("0400000000000000"), At(@"SYSTEM\CurrentControlSet\Control\AppID\Configuration\SMARTLOCKER", "ENABLED"));
            Assert.Equal(RegistryValue.DWord(1), At(@"SYSTEM\CurrentControlSet\Control\CI\Policy", "VerifiedAndReputablePolicyState"));

            Assert.Equal(RegistryValue.DWord(0), At(@"SOFTWARE\Microsoft\Windows Defender Security Center\Notifications", "DisableEnhancedNotifications"));
            Assert.Equal(RegistryValue.DWord(0), At(@"SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection", "NoActionNotificationDisabled"));
            Assert.Equal(RegistryValue.DWord(0), At(@"SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection", "SummaryNotificationDisabled"));
            Assert.Equal(RegistryValue.DWord(0), At(@"SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection", "FilesBlockedNotificationDisabled"));
            Assert.Equal(RegistryValue.DWord(1228), At(@"SYSTEM\ControlSet001\Services\SharedAccess\Epoch", "Epoch"));
            Assert.Equal(RegistryValue.DWord(0), At(@"SYSTEM\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile", "DisableNotifications"));
            Assert.Equal(RegistryValue.DWord(0), At(@"SYSTEM\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile", "DisableNotifications"));
            Assert.Equal(RegistryValue.DWord(0), At(@"SYSTEM\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile", "DisableNotifications"));
            Assert.Equal(RegistryValue.String("Warn"), At(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled"));

            // Windows' own default, which is the value being absent.
            Assert.Null(At(@"SYSTEM\ControlSet001\Control\Session Manager\kernel", "MitigationOptions"));

            Assert.Equal(RegistryValue.DWord(1), At(@"SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled"));
            Assert.Equal(RegistryValue.DWord(2), At(@"SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "WasEnabledBy"));
            Assert.Null(At(@"SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "ChangedInBootCycle"));
            // AkariOS's enable list leaves VBS off; the spec restores it, its hypervisor boot setting too.
            Assert.Equal(RegistryValue.DWord(1), At(@"SYSTEM\ControlSet001\Control\DeviceGuard", "EnableVirtualizationBasedSecurity"));
            Assert.Equal(new BootSettingValue("Auto"), At(new BootSettingLocation("hypervisorlaunchtype")));
            Assert.Null(At(new BootSettingLocation("isolatedcontext")));
            Assert.Null(At(new BootSettingLocation("allowedinmemorysettings")));
            Assert.Equal(RegistryValue.DWord(2), At(@"SYSTEM\CurrentControlSet\Control\Lsa", "RunAsPPL"));
            Assert.Equal(RegistryValue.DWord(1), At(@"SYSTEM\ControlSet001\Control\CI\Config", "VulnerableDriverBlocklistEnable"));

            Assert.Equal(RegistryValue.DWord(0), At(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection", "DisableNotifications")));
            Assert.Equal(RegistryValue.DWord(0), At(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection", "DisableDynamiclockNotifications")));
            Assert.Equal(RegistryValue.DWord(0), At(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection", "DisableWindowsHelloNotifications")));
            Assert.Equal(RegistryValue.DWord(1), At(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenEnabled", "")));
            Assert.Equal(RegistryValue.DWord(1), At(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenPuaEnabled", "")));
            Assert.Equal(RegistryValue.DWord(1), At(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost", "EnableWebContentEvaluation")));

            Assert.All(Tasks, task => Assert.Equal(TaskEnabledValue.On, Option("on").Values[task]));
            Assert.All(Services, service => Assert.Equal(RegistryValue.DWord(service.On), Option("on").Values[Service(service.Name)]));
        }
        finally
        {
            CurrentOption = "off";
        }
    }

    // The tray icon, the right-click menu entries, the tasks and the current-user values are written by
    // Akari-Dash in the session that asked, because safe mode may sign in as someone else.
    [Fact]
    public void Off_and_on_own_the_tray_and_the_menu_entries_in_the_own_session()
    {
        var epp = Tweak.Targets
            .Where(target => target.Location is RegistryLocation { Key: var key } && key.EndsWith(@"shellex\ContextMenuHandlers\EPP"))
            .ToList();

        Assert.Equal(3, epp.Count);
        Assert.All(epp, target =>
        {
            Assert.True(target.WrittenInOwnSession);
            Assert.Equal("", ((RegistryLocation)target.Location).Name);
            Assert.Null(Option("off").Values[target]);
            Assert.Equal(RegistryValue.String("{09A47860-11B0-4DA5-AFA5-26D86198A780}"), Option("on").Values[target]);
            Assert.Equal(RegistryValue.String("{09A47860-11B0-4DA5-AFA5-26D86198A780}"), target.AbsentMeans);
        });

        var tray = new RegistryValue(RegistryValueKind.ExpandString, @"%windir%\system32\SecurityHealthSystray.exe");
        var securityHealth = Tweak.Targets.Single(target => target.Location == new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "SecurityHealth"));

        Assert.True(securityHealth.WrittenInOwnSession);
        Assert.Null(Option("off").Values[securityHealth]);
        Assert.Equal(tray, Option("on").Values[securityHealth]);
        Assert.Equal(tray, securityHealth.AbsentMeans);

        // The rest of the own-session set: the four tasks, the three account protection reminders and
        // the three Edge/AppHost SmartScreen values.
        var ownSession = Tweak.Targets.Where(target => target.WrittenInOwnSession).ToList();
        Assert.Equal(14, ownSession.Count);
        Assert.Equal(4, ownSession.Count(target => target.Location is ScheduledTaskLocation));
        Assert.Equal(3, ownSession.Count(target => target.Location is RegistryLocation { Hive: RegistryHive.CurrentUser, Key: @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection" }));
        Assert.Equal(3, ownSession.Count(target => target.Location is RegistryLocation { Hive: RegistryHive.CurrentUser, Key: @"SOFTWARE\Microsoft\Edge\SmartScreenEnabled" or @"SOFTWARE\Microsoft\Edge\SmartScreenPuaEnabled" or @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost" }));
        Assert.Equal(1, ownSession.Count(target => target == securityHealth));
        Assert.Equal(3, epp.Count);
    }

    [Fact]
    public void The_pass_targets_are_the_rest()
    {
        var passTargets = DefenderOptimize.PassTargets(Tweak).Select(target => target.Location).ToList();

        Assert.Equal(56, Tweak.Targets.Count);
        Assert.Equal(42, passTargets.Count);
        Assert.Equal(42, Tweak.Targets.Count(target => !target.WrittenInOwnSession));
        Assert.DoesNotContain(passTargets, location => location is ScheduledTaskLocation);
        Assert.DoesNotContain(passTargets.OfType<RegistryLocation>(), location => location.Hive == RegistryHive.CurrentUser);
        Assert.DoesNotContain(passTargets.OfType<RegistryLocation>(), location => location.Key.EndsWith(@"shellex\ContextMenuHandlers\EPP"));
        Assert.DoesNotContain(passTargets.OfType<RegistryLocation>(), location => location.Key == @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
    }

    [Fact]
    public void Applying_off_stages_a_pass_and_writes_nothing_safe_mode()
    {
        var machine = DefenderMachine();
        var store = new InMemoryOriginalValuesStore();
        var engine = new TweakEngine(machine, store);

        engine.Apply(Tweak, Option("off"));

        var pass = Assert.Single(machine.SafeModePasses);
        Assert.Equal(new SafeModePass("security.disable-defender", "off", Undo: false), pass);
        Assert.True(engine.IsApplied(Tweak));
        Assert.Equal("off", engine.AppliedOption(Tweak)!.Id);

        // Nothing Windows only lets TrustedInstaller write reached the machine.
        Assert.Null(machine.Read(Tweak.Targets[0].Location));
    }

    [Fact]
    public void Applying_off_writes_the_own_session_values_now()
    {
        var machine = DefenderMachine();
        var engine = new TweakEngine(machine, new InMemoryOriginalValuesStore());

        engine.Apply(Tweak, Option("off"));

        Assert.Equal(RegistryValue.DWord(0), machine.Read(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenEnabled", "")));
        Assert.Null(machine.Read(new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Classes\Directory\shellex\ContextMenuHandlers\EPP", "")));
        Assert.Null(machine.Read(new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "SecurityHealth")));
        Assert.Equal(TaskEnabledValue.Off, machine.Read(new ScheduledTaskLocation(@"\Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan")));

        Assert.Single(machine.SafeModePasses);
    }

    [Fact]
    public void Undoing_stages_a_pass_and_keeps_the_original_values()
    {
        var machine = DefenderMachine();
        var store = new InMemoryOriginalValuesStore();
        var engine = new TweakEngine(machine, store);
        engine.Apply(Tweak, Option("off"));

        engine.Undo(Tweak);

        Assert.Equal(new SafeModePass("security.disable-defender", "off", Undo: true), machine.SafeModePasses[1]);
        Assert.NotNull(store.Get("security.disable-defender"));
        Assert.True(engine.IsApplied(Tweak));
    }

    [Fact]
    public void A_staged_pass_is_pending_until_the_restart()
    {
        var engine = new TweakEngine(DefenderMachine(), new InMemoryOriginalValuesStore());

        engine.Apply(Tweak, Option("off"));

        var pending = engine.Pending;
        Assert.True(pending.NeedsRestart);
        Assert.Equal(Tweak, Assert.Single(pending.AfterRestart));
        Assert.Empty(pending.AfterSignOut);
    }

    [Fact]
    public void A_dry_run_previews_without_staging()
    {
        var on = Option("on");
        var machine = new InMemoryMachine();

        foreach (var (target, value) in on.Values)
        {
            if (value is not null)
            {
                machine.With(target.Location, value);
            }
        }

        var dryRun = new DryRunMachine(machine);
        var engine = new TweakEngine(machine, new InMemoryOriginalValuesStore(), applyTo: dryRun);

        var preview = engine.Preview(Tweak, Option("off"));
        engine.Apply(Tweak, Option("off"));

        Assert.Empty(machine.SafeModePasses);

        // Off writes a different value from On for everything except memory integrity's bookkeeping and
        // the two hypervisor boot settings, which both sides leave absent: null to null is nothing to
        // show.
        var nullToNull = Tweak.Targets.Count(target => Option("off").Values[target] is null && on.Values[target] is null);
        Assert.Equal(3, nullToNull);
        Assert.Equal(Tweak.Targets.Count - nullToNull, preview.Count);

        // The eleven Start changes and the four menu-and-tray deletes are in it.
        Assert.Equal(Services.Length, preview.Count(change => change.Location is RegistryLocation { Name: "Start" }));
        Assert.Equal(4, preview.Count(change => change.Location is RegistryLocation location
            && (location.Key.EndsWith(@"shellex\ContextMenuHandlers\EPP") || location.Key == @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run")));
    }

    // A PC Defender has never been turned off on reads as On: every value On writes is where a PC
    // Windows has not touched it is, and what On leaves absent is absent.
    [Fact]
    public void An_untouched_pc_reads_as_on_rather_than_off()
    {
        var machine = new InMemoryMachine();

        foreach (var (target, value) in Option("on").Values)
        {
            if (value is not null)
            {
                machine.With(target.Location, value);
            }
        }

        var state = new TweakEngine(machine).ReadLiveState(Tweak);

        Assert.Equal("on", Assert.IsType<LiveState.InOption>(state).Option.Id);
    }

    // The largest edition hole: Home has no Sense and no webthreatdefsvc, so those Start values are
    // absent. Every Start target counts a missing value as what Windows ships (On), so the row still
    // reads On rather than Custom.
    [Fact]
    public void Absent_services_read_as_windows_default()
    {
        var machine = new InMemoryMachine();
        var on = Option("on");

        foreach (var (target, value) in on.Values)
        {
            if (value is not null && target.Location is not RegistryLocation { Name: "Start" })
            {
                machine.With(target.Location, value);
            }
        }

        var state = new TweakEngine(machine).ReadLiveState(Tweak);

        Assert.Equal("on", Assert.IsType<LiveState.InOption>(state).Option.Id);
    }
}
