using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

// Security Tweaks: the parts of AkariOS-Ultimate's Defender Optimize (MIT, Tweaks/Windows.ps1) that
// are registry values or boot settings, each with its own Undo. Turning Defender itself off (real-time,
// cloud and tamper protection) needs safe boot and TrustedInstaller, so it is a separate Coded Tweak.
// Phishing protection (WTDS\Components) goes with it too: only SYSTEM and TrustedInstaller may even
// read that key. Smart App Control is left out: once off, Windows cannot turn it back on without a
// reinstall. None
// has a Recommended Option: each trades protection for speed or quiet.
public static partial class TweakCatalog
{
    private const string DeviceGuardKey = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
    private const string SecurityCenterKey = @"SOFTWARE\Microsoft\Windows Defender Security Center";

    private static IEnumerable<DeclaredTweak> SecurityTweaks() =>
    [
        MemoryIntegrity(),
        VirtualizationBasedSecurity(),
        LsaProtection(),
        VulnerableDriverBlocklist(),
        SmartScreenForAppsAndFiles(),
        SmartScreenForEdge(),
        SmartScreenForStoreApps(),
        ExploitProtection(),
        DefenderScheduledTasks(),
        SecurityNotifications(),
        FirewallNotifications(),
    ];

    private static DeclaredTweak SecurityTweak(
        string id, string title, string description, string group, TweakTarget[] targets, TweakOption[] options,
        Activation activation = Activation.AfterRestart) =>
        new(id, title, description, Category.Security, group, targets, options, Activation: activation);

    private static DeclaredTweak MemoryIntegrity()
    {
        // Defender Optimize: Enabled 0 and the two bookkeeping values deleted; its default puts back
        // Enabled 1 and WasEnabledBy 2 (enabled by the user). A missing Enabled counts as off.
        TweakTarget[] targets =
        [
            new(new RegistryLocation(RegistryHive.LocalMachine, $@"{DeviceGuardKey}\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled"), AbsentMeans: RegistryValue.DWord(0)),
            new(new RegistryLocation(RegistryHive.LocalMachine, $@"{DeviceGuardKey}\Scenarios\HypervisorEnforcedCodeIntegrity", "WasEnabledBy")),
            new(new RegistryLocation(RegistryHive.LocalMachine, $@"{DeviceGuardKey}\Scenarios\HypervisorEnforcedCodeIntegrity", "ChangedInBootCycle")),
        ];

        return SecurityTweak("security.memory-integrity", "Memory integrity",
            "Core isolation's memory integrity checks every driver inside a protected part of memory, which blocks some attacks but costs a few frames per second in many games. Some anti-cheats (FACEIT, Vanguard) need it on.",
            "Core isolation", targets,
            [
                Values("off", "Off", RegistryValue.DWord(0), null, null),
                Values("on", "On", RegistryValue.DWord(1), RegistryValue.DWord(2), null),
            ]);

        TweakOption Values(string id, string label, MachineValue? enabled, MachineValue? wasEnabledBy, MachineValue? changed) => new(
            id, label, new Dictionary<TweakTarget, MachineValue?> { [targets[0]] = enabled, [targets[1]] = wasEnabledBy, [targets[2]] = changed });
    }

    private static DeclaredTweak VirtualizationBasedSecurity()
    {
        // Defender Optimize turns VBS fully off: it deletes the hypervisor boot settings (so nothing,
        // not even an anti-cheat that sets the policy again, can start VBS) and the policy value; here
        // the policy is set to 0, Microsoft's documented off. Turning the hypervisor off also stops
        // WSL2, Hyper-V and Windows Sandbox. Undo puts the original boot settings back.
        TweakTarget[] targets =
        [
            new(new RegistryLocation(RegistryHive.LocalMachine, DeviceGuardKey, "EnableVirtualizationBasedSecurity")),
            new(new BootSettingLocation("hypervisorlaunchtype")),
            new(new BootSettingLocation("isolatedcontext")),
            new(new BootSettingLocation("allowedinmemorysettings")),
        ];

        return SecurityTweak("security.vbs", "Virtualization-based security",
            "Runs part of Windows in a protected virtual machine, which memory integrity, Credential Guard and other protections build on. Off turns VBS and the Windows hypervisor fully off, which can raise game performance, but also stops WSL2, Hyper-V and Windows Sandbox. Turn memory integrity off too.",
            "Core isolation", targets,
            [
                new TweakOption("off", "Off", new Dictionary<TweakTarget, MachineValue?>
                {
                    [targets[0]] = RegistryValue.DWord(0), [targets[1]] = null, [targets[2]] = null, [targets[3]] = null,
                }),
                new TweakOption("windows-decides", "Let Windows decide", new Dictionary<TweakTarget, MachineValue?>
                {
                    [targets[0]] = null, [targets[1]] = new BootSettingValue("Auto"), [targets[2]] = null, [targets[3]] = null,
                }),
            ]);
    }

    private static DeclaredTweak LsaProtection()
    {
        // Defender Optimize sets RunAsPPL to 0; its default sets 2 (on, without a UEFI lock).
        var runAsPpl = new TweakTarget(new RegistryLocation(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Lsa", "RunAsPPL"));

        return SecurityTweak("security.lsa-protection", "Local Security Authority protection",
            "Runs the process that holds your sign-in secrets as a protected process, so other programs cannot read them. Off lets older drivers and tools load into it.",
            "Core isolation", [runAsPpl],
            [DWordOption(runAsPpl, "off", "Off", 0), DWordOption(runAsPpl, "on", "On", 2)]);
    }

    private static DeclaredTweak VulnerableDriverBlocklist()
    {
        // Defender Optimize sets VulnerableDriverBlocklistEnable to 0; Windows 11 treats missing as on.
        var enabled = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\CI\Config", "VulnerableDriverBlocklistEnable"),
            AbsentMeans: RegistryValue.DWord(1));

        return SecurityTweak("security.driver-blocklist", "Microsoft vulnerable driver blocklist",
            "Stops drivers with known security holes from loading. Off lets them load, which some older hardware tools and overclocking utilities need.",
            "Core isolation", [enabled],
            [DWordOption(enabled, "off", "Off", 0), DWordOption(enabled, "on", "On", 1)]);
    }

    private static DeclaredTweak SmartScreenForAppsAndFiles()
    {
        // Defender Optimize: SmartScreenEnabled "Off"; its default "Warn".
        var smartScreen = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled"),
            AbsentMeans: RegistryValue.String("Warn"));

        return SecurityTweak("security.smartscreen-apps", "Check apps and files",
            "SmartScreen checks apps and files you download against Microsoft's list of known-bad ones, and warns before they run.",
            "SmartScreen", [smartScreen],
            [Option(smartScreen, "off", "Off", RegistryValue.String("Off")), Option(smartScreen, "warn", "Warn", RegistryValue.String("Warn"))],
            Activation.Immediately);
    }

    private static DeclaredTweak SmartScreenForEdge()
    {
        // Defender Optimize: the default values of Edge\SmartScreenEnabled and SmartScreenPuaEnabled, 0 or 1.
        TweakTarget[] targets =
        [
            new(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenEnabled", ""), AbsentMeans: RegistryValue.DWord(1)),
            new(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenPuaEnabled", ""), AbsentMeans: RegistryValue.DWord(1)),
        ];

        return SecurityTweak("security.smartscreen-edge", "SmartScreen for Microsoft Edge",
            "Edge checks the sites you visit and the files you download, including unwanted apps, against Microsoft's lists.",
            "SmartScreen", targets,
            [DWordOption(targets, "off", "Off", 0), DWordOption(targets, "on", "On", 1)],
            Activation.Immediately);
    }

    private static DeclaredTweak SmartScreenForStoreApps()
    {
        // Defender Optimize: AppHost EnableWebContentEvaluation 0, default 1.
        var evaluation = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost", "EnableWebContentEvaluation"),
            AbsentMeans: RegistryValue.DWord(1));

        return SecurityTweak("security.smartscreen-store", "SmartScreen for Microsoft Store apps",
            "Checks the web content Store apps load against Microsoft's list of known-bad sites.",
            "SmartScreen", [evaluation],
            [DWordOption(evaluation, "off", "Off", 0), DWordOption(evaluation, "on", "On", 1)],
            Activation.Immediately);
    }

    private static DeclaredTweak ExploitProtection()
    {
        // Defender Optimize writes this MitigationOptions mask, which turns off system-wide exploit
        // mitigations but keeps Control Flow Guard on, as Vanguard needs. Windows uses its own
        // defaults when the value is missing.
        var mitigations = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "MitigationOptions"));

        return SecurityTweak("security.exploit-protection", "Exploit protection",
            "System-wide protections against attacks on bugs in apps, such as data execution prevention and address randomisation. Off keeps Control Flow Guard on, which Vanguard needs.",
            "Exploit protection", [mitigations],
            [
                Option(mitigations, "off", "Off (keep Control Flow Guard)", new RegistryValue(RegistryValueKind.Binary, Convert.FromHexString("222222000002000000020000000000000000000000000000"))),
                Option(mitigations, "windows-default", "Windows default", null),
            ]);
    }

    private static DeclaredTweak DefenderScheduledTasks()
    {
        // Defender Optimize disables these four in normal boot as admin; Windows ships them enabled.
        TweakTarget[] targets =
        [
            .. new[] { "Windows Defender Cache Maintenance", "Windows Defender Cleanup", "Windows Defender Scheduled Scan", "Windows Defender Verification" }
                .Select(task => new TweakTarget(new ScheduledTaskLocation($@"\Microsoft\Windows\Windows Defender\{task}"))),
        ];

        return SecurityTweak("security.defender-tasks", "Defender scheduled tasks",
            "Defender's background scan, cache upkeep, cleanup and update check, which can start while you play. Off stops them; real-time protection still scans files as they are opened.",
            "Microsoft Defender", targets,
            [Option(targets, "off", "Off", TaskEnabledValue.Off), Option(targets, "on", "On", TaskEnabledValue.On)],
            Activation.Immediately);
    }

    private static DeclaredTweak SecurityNotifications()
    {
        // Defender Optimize sets these to 1; its default sets them to 0, which missing values also mean.
        TweakTarget[] targets =
        [
            Disable(RegistryHive.LocalMachine, "Notifications", "DisableEnhancedNotifications"),
            Disable(RegistryHive.LocalMachine, "Virus and threat protection", "NoActionNotificationDisabled"),
            Disable(RegistryHive.LocalMachine, "Virus and threat protection", "SummaryNotificationDisabled"),
            Disable(RegistryHive.LocalMachine, "Virus and threat protection", "FilesBlockedNotificationDisabled"),
            Disable(RegistryHive.CurrentUser, "Account protection", "DisableNotifications"),
            Disable(RegistryHive.CurrentUser, "Account protection", "DisableDynamiclockNotifications"),
            Disable(RegistryHive.CurrentUser, "Account protection", "DisableWindowsHelloNotifications"),
        ];

        return SecurityTweak("security.security-notifications", "Windows Security notifications",
            "Summaries, \"no action needed\" messages, blocked-file messages and account protection reminders from Windows Security. Off keeps important alerts, such as threats found.",
            "Notifications", targets,
            [DWordOption(targets, "off", "Off", 1), DWordOption(targets, "on", "On", 0)],
            Activation.AfterSignOut);

        static TweakTarget Disable(RegistryHive hive, string area, string name) =>
            new(new RegistryLocation(hive, $@"{SecurityCenterKey}\{area}", name), AbsentMeans: RegistryValue.DWord(0));
    }

    private static DeclaredTweak FirewallNotifications()
    {
        // Defender Optimize sets DisableNotifications to 1 for the three firewall profiles; its default
        // sets 0, which a missing value also means. It also bumps the firewall's Epoch counter, which
        // only tells the firewall to re-read its settings, so it is left out here.
        TweakTarget[] targets =
        [
            .. new[] { "DomainProfile", "PublicProfile", "StandardProfile" }
                .Select(profile => new TweakTarget(
                    new RegistryLocation(RegistryHive.LocalMachine, $@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\{profile}", "DisableNotifications"),
                    AbsentMeans: RegistryValue.DWord(0))),
        ];

        return SecurityTweak("security.firewall-notifications", "Firewall notifications",
            "Windows Firewall tells you when it blocks a new app. Off blocks it quietly.",
            "Notifications", targets,
            [DWordOption(targets, "off", "Off", 1), DWordOption(targets, "on", "On", 0)]);
    }
}
