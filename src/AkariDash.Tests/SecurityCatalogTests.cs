using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

/// <summary>The Security page: the registry and boot-setting parts of Defender Optimize (issue #26).</summary>
public class SecurityCatalogTests
{
    private static readonly IReadOnlyList<DeclaredTweak> Security =
        TweakCatalog.All.Where(tweak => tweak.Category == Category.Security).ToList();

    private static DeclaredTweak Find(string id) => Security.Single(tweak => tweak.Id == id);

    [Fact]
    public void Security_page_has_its_groups_and_no_recommended_option()
    {
        Assert.Equal(["Core isolation", "SmartScreen", "Exploit protection", "Microsoft Defender", "Notifications"], Security.Select(tweak => tweak.Group).Distinct());
        Assert.All(Security, tweak =>
        {
            Assert.StartsWith("security.", tweak.Id);
            Assert.Null(tweak.Recommended);
        });
    }

    [Fact]
    public void Vbs_off_sets_the_policy_to_zero_and_deletes_the_hypervisor_boot_settings()
    {
        var vbs = Find("security.vbs");
        var off = vbs.Options.Single(option => option.Id == "off");

        Assert.Equal(RegistryValue.DWord(0), off.Values[vbs.Targets[0]]);
        Assert.Equal(
            ["hypervisorlaunchtype", "isolatedcontext", "allowedinmemorysettings"],
            vbs.Targets.Skip(1).Select(target => ((BootSettingLocation)target.Location).Name));
        Assert.All(vbs.Targets.Skip(1), target => Assert.Null(off.Values[target]));
    }

    // A real PC runs VBS and memory integrity with no hypervisor boot setting at all, so a missing
    // hypervisorlaunchtype has to read as the Auto it behaves as, not as Custom.
    [Fact]
    public void Vbs_on_a_machine_with_no_boot_settings_at_all_reads_as_letting_windows_decide()
    {
        var vbs = Find("security.vbs");

        var state = new TweakEngine(new InMemoryMachine()).ReadLiveState(vbs);

        Assert.Equal("windows-decides", Assert.IsType<LiveState.InOption>(state).Option.Id);
        Assert.Equal(new BootSettingValue("Auto"), vbs.Targets[1].AbsentMeans);
        Assert.All(vbs.Targets.Where((_, index) => index != 1), target => Assert.Null(target.AbsentMeans));
    }

    // Windows Security's "Reset all to default" writes its defaults out as an explicit all-enabled
    // mask instead of deleting the value, so both it and a missing value read as Windows' default.
    [Fact]
    public void Exploit_protection_reads_windows_default_whether_the_mask_is_absent_or_written_out()
    {
        var mitigations = Find("security.exploit-protection").Targets.Single();
        var written = new InMemoryMachine().With(mitigations.Location, mitigations.AbsentMeans!);

        var absentState = new TweakEngine(new InMemoryMachine()).ReadLiveState(Find("security.exploit-protection"));
        var writtenState = new TweakEngine(written).ReadLiveState(Find("security.exploit-protection"));

        Assert.Equal("windows-default", Assert.IsType<LiveState.InOption>(absentState).Option.Id);
        Assert.Equal("windows-default", Assert.IsType<LiveState.InOption>(writtenState).Option.Id);
        Assert.Equal(
            new RegistryValue(Microsoft.Win32.RegistryValueKind.Binary, Convert.FromHexString("111111000001000000000000000000000000000000000000")),
            mitigations.AbsentMeans);
    }

    // A mask the user built by hand is neither Option, and saying so is the point of Custom.
    [Fact]
    public void Exploit_protection_reads_custom_for_a_hand_built_mask()
    {
        var tweak = Find("security.exploit-protection");
        var machine = new InMemoryMachine().With(
            tweak.Targets.Single().Location,
            new RegistryValue(Microsoft.Win32.RegistryValueKind.Binary, Convert.FromHexString("121212000001000000000000000000000000000000000000")));

        var state = new TweakEngine(machine).ReadLiveState(tweak);

        Assert.IsType<LiveState.Custom>(state);
    }

    [Fact]
    public void Memory_integrity_off_and_on_match_defender_optimize_and_its_default()
    {
        var tweak = Find("security.memory-integrity");
        var off = tweak.Options.Single(option => option.Id == "off");
        var on = tweak.Options.Single(option => option.Id == "on");

        Assert.Equal([RegistryValue.DWord(0), null, null], tweak.Targets.Select(target => off.Values[target]));
        Assert.Equal([RegistryValue.DWord(1), RegistryValue.DWord(2), null], tweak.Targets.Select(target => on.Values[target]));
    }

    // Defender's own keys and Smart App Control used to be left off this page because Windows only
    // lets them be written in safe boot as TrustedInstaller. They are two rows now — Defender optimize
    // and Disable Defender — both written by the companion; the rest of the page still must not touch
    // them a second time.
    [Fact]
    public void Only_the_safe_mode_rows_own_defenders_own_keys_and_smart_app_control()
    {
        string[] safeModeRows = ["security.defender-optimize", "security.disable-defender"];
        var others = Security
            .Where(tweak => !safeModeRows.Contains(tweak.Id))
            .SelectMany(tweak => tweak.Targets)
            .Select(target => target.Location)
            .OfType<RegistryLocation>()
            .Select(location => location.Key);

        Assert.DoesNotContain(others, key => key.Contains(@"Windows Defender\Real-Time Protection") || key.Contains("SMARTLOCKER"));
    }

    [Fact]
    public void Defender_optimize_is_the_microsoft_defender_row_written_in_safe_mode()
    {
        var tweak = Find("security.defender-optimize");

        Assert.Equal("Microsoft Defender", tweak.Group);
        Assert.Equal(Activation.InSafeMode, tweak.Activation);
        Assert.Null(tweak.Recommended);
    }

    // AkariOS-Ultimate's 'defender' ("Defender optimize"), Tweaks/Windows.ps1:5705: one toggle that owns
    // the whole Tweak, so the Security rows beneath it hold values this one also writes.
    [Fact]
    public void Defender_optimize_writes_what_akarios_defender_optimize_writes()
    {
        var tweak = Find("security.defender-optimize");
        var optimize = tweak.Options.Single(option => option.Id == "optimize");

        Assert.Equal(RegistryValue.DWord(0), Optimized(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring"));
        Assert.Equal(RegistryValue.DWord(1), Optimized(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableAsyncScanOnOpen"));
        Assert.Equal(RegistryValue.DWord(0), Optimized(@"SOFTWARE\Microsoft\Windows Defender\Spynet", "SpyNetReporting"));
        Assert.Equal(RegistryValue.DWord(0), Optimized(@"SOFTWARE\Microsoft\Windows Defender\Spynet", "SubmitSamplesConsent"));
        Assert.Equal(RegistryValue.DWord(4), Optimized(@"SOFTWARE\Microsoft\Windows Defender\Features", "TamperProtection"));
        Assert.Equal(RegistryValue.DWord(0), Optimized(@"SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access", "EnableControlledFolderAccess"));
        Assert.Equal(RegistryValue.DWord(0), Optimized(@"SOFTWARE\Microsoft\Windows Defender", "PUAProtection"));
        Assert.Equal(RegistryValue.DWord(0), Optimized(@"SOFTWARE\Microsoft\Windows Defender", "VerifiedAndReputableTrustModeEnabled"));
        Assert.Equal(RegistryValue.DWord(0), Optimized(@"SOFTWARE\Microsoft\Windows Defender", "SmartLockerMode"));
        Assert.Equal(RegistryValue.DWord(0), Optimized(@"SYSTEM\CurrentControlSet\Control\AppID\Configuration\SMARTLOCKER", "START_PENDING"));
        Assert.Equal(Binary("0000000000000000"), Optimized(@"SYSTEM\CurrentControlSet\Control\AppID\Configuration\SMARTLOCKER", "ENABLED"));
        Assert.Equal(RegistryValue.DWord(0), Optimized(@"SYSTEM\CurrentControlSet\Control\CI\Policy", "VerifiedAndReputablePolicyState"));

        // Real-time protection stays ON: AkariOS's Optimize sets DisableRealtimeMonitoring 0, not 1,
        // because Windows turns it back on by itself.

        // Windows Security's own notifications, the firewall's, and the account protection reminders.
        Assert.Equal(RegistryValue.DWord(1), Optimized(@"SOFTWARE\Microsoft\Windows Defender Security Center\Notifications", "DisableEnhancedNotifications"));
        Assert.Equal(RegistryValue.DWord(1), Optimized(@"SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection", "NoActionNotificationDisabled"));
        Assert.Equal(RegistryValue.DWord(1), Optimized(@"SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection", "SummaryNotificationDisabled"));
        Assert.Equal(RegistryValue.DWord(1), Optimized(@"SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection", "FilesBlockedNotificationDisabled"));
        Assert.Equal(RegistryValue.DWord(1), OptimizedAt(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection", "DisableNotifications")));
        Assert.Equal(RegistryValue.DWord(1), OptimizedAt(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection", "DisableDynamiclockNotifications")));
        Assert.Equal(RegistryValue.DWord(1), OptimizedAt(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection", "DisableWindowsHelloNotifications")));

        // The firewall's Epoch counter, which only tells the firewall to re-read its settings, and the
        // three profiles it reads them for.
        Assert.Equal(RegistryValue.DWord(1231), Optimized(@"SYSTEM\ControlSet001\Services\SharedAccess\Epoch", "Epoch"));
        Assert.Equal(RegistryValue.DWord(1), Optimized(@"SYSTEM\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile", "DisableNotifications"));
        Assert.Equal(RegistryValue.DWord(1), Optimized(@"SYSTEM\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile", "DisableNotifications"));
        Assert.Equal(RegistryValue.DWord(1), Optimized(@"SYSTEM\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile", "DisableNotifications"));

        // SmartScreen for apps and files, Edge and Store apps: current-user ones need no safe boot.
        Assert.Equal(RegistryValue.String("Off"), Optimized(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled"));
        Assert.Equal(RegistryValue.DWord(0), OptimizedAt(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenEnabled", "")));
        Assert.Equal(RegistryValue.DWord(0), OptimizedAt(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenPuaEnabled", "")));
        Assert.Equal(RegistryValue.DWord(0), OptimizedAt(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost", "EnableWebContentEvaluation")));

        // Exploit protection's mask, VBS and memory integrity's policy and bookkeeping, LSA protection
        // and the driver blocklist.
        Assert.Equal(Binary("222222000002000000020000000000000000000000000000"), Optimized(@"SYSTEM\ControlSet001\Control\Session Manager\kernel", "MitigationOptions"));
        Assert.Equal(RegistryValue.DWord(0), Optimized(@"SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled"));
        Assert.Null(Optimized(@"SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "WasEnabledBy"));
        Assert.Null(Optimized(@"SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "ChangedInBootCycle"));
        Assert.Null(Optimized(@"SYSTEM\ControlSet001\Control\DeviceGuard", "EnableVirtualizationBasedSecurity"));
        Assert.Null(OptimizedAt(new BootSettingLocation("hypervisorlaunchtype")));
        Assert.Null(OptimizedAt(new BootSettingLocation("isolatedcontext")));
        Assert.Null(OptimizedAt(new BootSettingLocation("allowedinmemorysettings")));
        Assert.Equal(RegistryValue.DWord(0), Optimized(@"SYSTEM\CurrentControlSet\Control\Lsa", "RunAsPPL"));
        Assert.Equal(RegistryValue.DWord(0), Optimized(@"SYSTEM\ControlSet001\Control\CI\Config", "VulnerableDriverBlocklistEnable"));

        // Defender's own background work, which tamper protection can put back on by itself.
        var tasks = tweak.Targets.Where(target => target.Location is ScheduledTaskLocation).ToList();
        Assert.Equal(4, tasks.Count);
        Assert.All(tasks, target => Assert.Equal(TaskEnabledValue.Off, optimize.Values[target]));

        Assert.Equal(41, tweak.Targets.Count);

        MachineValue? Optimized(string key, string name) =>
            optimize.Values[tweak.Targets.Single(target => target.Location == new RegistryLocation(RegistryHive.LocalMachine, key, name))];

        MachineValue? OptimizedAt(MachineLocation location) =>
            optimize.Values[tweak.Targets.Single(target => target.Location == location)];
    }

    // AkariOS's -Revert, value for value. Potentially unwanted app blocking is written twice there,
    // 2 (audit only) then 1 (on), so 1 is the value it settles on and the one asserted here.
    [Fact]
    public void Defender_optimize_windows_default_is_what_akarios_revert_writes()
    {
        var tweak = Find("security.defender-optimize");
        var windowsDefault = tweak.Options.Single(option => option.Id == "windows-default");

        Assert.Equal(RegistryValue.DWord(0), Default(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring"));
        Assert.Equal(RegistryValue.DWord(0), Default(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableAsyncScanOnOpen"));
        Assert.Equal(RegistryValue.DWord(2), Default(@"SOFTWARE\Microsoft\Windows Defender\Spynet", "SpyNetReporting"));
        Assert.Equal(RegistryValue.DWord(1), Default(@"SOFTWARE\Microsoft\Windows Defender\Spynet", "SubmitSamplesConsent"));
        Assert.Equal(RegistryValue.DWord(5), Default(@"SOFTWARE\Microsoft\Windows Defender\Features", "TamperProtection"));
        Assert.Equal(RegistryValue.DWord(1), Default(@"SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access", "EnableControlledFolderAccess"));
        Assert.Equal(RegistryValue.DWord(1), Default(@"SOFTWARE\Microsoft\Windows Defender", "PUAProtection"));
        Assert.Equal(RegistryValue.DWord(1), Default(@"SOFTWARE\Microsoft\Windows Defender", "VerifiedAndReputableTrustModeEnabled"));
        Assert.Equal(RegistryValue.DWord(1), Default(@"SOFTWARE\Microsoft\Windows Defender", "SmartLockerMode"));
        Assert.Equal(RegistryValue.DWord(4), Default(@"SYSTEM\CurrentControlSet\Control\AppID\Configuration\SMARTLOCKER", "START_PENDING"));
        Assert.Equal(Binary("0400000000000000"), Default(@"SYSTEM\CurrentControlSet\Control\AppID\Configuration\SMARTLOCKER", "ENABLED"));
        Assert.Equal(RegistryValue.DWord(1), Default(@"SYSTEM\CurrentControlSet\Control\CI\Policy", "VerifiedAndReputablePolicyState"));

        // Windows Security's own notifications and the firewall's go back to telling you, and SmartScreen
        // for apps, Edge and Store apps goes back to warning.
        Assert.Equal(RegistryValue.DWord(0), Default(@"SOFTWARE\Microsoft\Windows Defender Security Center\Notifications", "DisableEnhancedNotifications"));
        Assert.Equal(RegistryValue.DWord(0), Default(@"SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection", "NoActionNotificationDisabled"));
        Assert.Equal(RegistryValue.DWord(0), Default(@"SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection", "SummaryNotificationDisabled"));
        Assert.Equal(RegistryValue.DWord(0), Default(@"SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection", "FilesBlockedNotificationDisabled"));
        Assert.Equal(RegistryValue.DWord(0), DefaultAt(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection", "DisableNotifications")));
        Assert.Equal(RegistryValue.DWord(0), DefaultAt(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection", "DisableDynamiclockNotifications")));
        Assert.Equal(RegistryValue.DWord(0), DefaultAt(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection", "DisableWindowsHelloNotifications")));
        Assert.Equal(RegistryValue.DWord(1228), Default(@"SYSTEM\ControlSet001\Services\SharedAccess\Epoch", "Epoch"));
        Assert.Equal(RegistryValue.DWord(0), Default(@"SYSTEM\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile", "DisableNotifications"));
        Assert.Equal(RegistryValue.DWord(0), Default(@"SYSTEM\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile", "DisableNotifications"));
        Assert.Equal(RegistryValue.DWord(0), Default(@"SYSTEM\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile", "DisableNotifications"));
        Assert.Equal(RegistryValue.String("Warn"), Default(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled"));
        Assert.Equal(RegistryValue.DWord(1), DefaultAt(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenEnabled", "")));
        Assert.Equal(RegistryValue.DWord(1), DefaultAt(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenPuaEnabled", "")));
        Assert.Equal(RegistryValue.DWord(1), DefaultAt(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost", "EnableWebContentEvaluation")));

        // Exploit protection's Windows-default mask, memory integrity on (as the user enabled it), VBS
        // back on, LSA protection locked and the driver blocklist back in force. AkariOS's own Revert
        // leaves VBS off; this one puts it back, because an Undo here is an Undo of the whole Tweak.
        Assert.Equal(Binary("111111000001000000000000000000000000000000000000"), Default(@"SYSTEM\ControlSet001\Control\Session Manager\kernel", "MitigationOptions"));
        Assert.Equal(RegistryValue.DWord(1), Default(@"SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled"));
        Assert.Equal(RegistryValue.DWord(2), Default(@"SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "WasEnabledBy"));
        Assert.Null(Default(@"SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "ChangedInBootCycle"));
        Assert.Equal(RegistryValue.DWord(1), Default(@"SYSTEM\ControlSet001\Control\DeviceGuard", "EnableVirtualizationBasedSecurity"));
        Assert.Equal(new BootSettingValue("Auto"), DefaultAt(new BootSettingLocation("hypervisorlaunchtype")));
        Assert.Null(DefaultAt(new BootSettingLocation("isolatedcontext")));
        Assert.Null(DefaultAt(new BootSettingLocation("allowedinmemorysettings")));
        Assert.Equal(RegistryValue.DWord(2), Default(@"SYSTEM\CurrentControlSet\Control\Lsa", "RunAsPPL"));
        Assert.Equal(RegistryValue.DWord(1), Default(@"SYSTEM\ControlSet001\Control\CI\Config", "VulnerableDriverBlocklistEnable"));

        var tasks = tweak.Targets.Where(target => target.Location is ScheduledTaskLocation).ToList();
        Assert.Equal(4, tasks.Count);
        Assert.All(tasks, target => Assert.Equal(TaskEnabledValue.On, windowsDefault.Values[target]));

        MachineValue? Default(string key, string name) =>
            windowsDefault.Values[tweak.Targets.Single(target => target.Location == new RegistryLocation(RegistryHive.LocalMachine, key, name))];

        MachineValue? DefaultAt(MachineLocation location) =>
            windowsDefault.Values[tweak.Targets.Single(target => target.Location == location)];
    }

    // A PC nothing has been done to reads as Windows default: neither Option is Custom, and the row is
    // not already showing Optimize before the user has asked for it. What the Windows default leaves
    // absent is left absent, which is where an untouched PC is.
    [Fact]
    public void An_untouched_pc_reads_as_windows_default_rather_than_optimize()
    {
        var tweak = Find("security.defender-optimize");
        var machine = new InMemoryMachine();
        var windowsDefault = tweak.Options.Single(option => option.Id == "windows-default");

        foreach (var (target, value) in windowsDefault.Values)
        {
            if (value is not null)
            {
                machine.With(target.Location, value);
            }
        }

        var state = new TweakEngine(machine).ReadLiveState(tweak);

        Assert.Equal("windows-default", Assert.IsType<LiveState.InOption>(state).Option.Id);
    }

    [Fact]
    public void Defender_tasks_are_the_four_defender_optimize_disables()
    {
        var tasks = Find("security.defender-tasks").Targets.Select(target => ((ScheduledTaskLocation)target.Location).Path);

        Assert.Equal(
            [
                @"\Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance",
                @"\Microsoft\Windows\Windows Defender\Windows Defender Cleanup",
                @"\Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan",
                @"\Microsoft\Windows\Windows Defender\Windows Defender Verification",
            ],
            tasks);
    }

    private static RegistryValue Binary(string hex) =>
        new(RegistryValueKind.Binary, Convert.FromHexString(hex));
}

