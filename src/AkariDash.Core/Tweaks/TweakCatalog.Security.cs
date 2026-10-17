using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

// Security Tweaks: the parts of AkariOS-Ultimate's Defender Optimize (MIT, Tweaks/Windows.ps1) that
// are registry values or boot settings, each with its own Undo. The settings Winhance keeps in the
// Security group of its Privacy & Security page (User Account Control, workplace join prompts,
// automatic device encryption, Wi-Fi Sense, error reporting, Remote Assistance) are where Winhance
// has them, in Privacy's own "Security" Group; see TweakCatalog.Privacy.cs. What Windows only lets
// be written in safe boot as TrustedInstaller (real-time and cloud protection, tamper protection,
// Smart App Control, and phishing protection) is DefenderOptimizeTweak below; the row DisableDefender
// builds covers those settings and Defender's own services and drivers too. The two are the Tweaks
// that stage a safe-mode pass, and neither has a Recommended Option: each trades protection for
// speed or quiet.
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
        DefenderOptimizeTweak(),
        DisableDefender(),
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
        //
        // A missing hypervisorlaunchtype is Auto, which is how Windows ships: a PC can run VBS and
        // memory integrity with no hypervisor boot setting at all, because memory integrity starts the
        // hypervisor by itself. So Auto is the setting's AbsentMeans, and "Let Windows decide" still
        // writes it, to force the hypervisor back on after Off deleted it.
        TweakTarget[] targets =
        [
            new(new RegistryLocation(RegistryHive.LocalMachine, DeviceGuardKey, "EnableVirtualizationBasedSecurity")),
            new(new BootSettingLocation("hypervisorlaunchtype"), AbsentMeans: new BootSettingValue("Auto")),
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
        // mitigations but keeps Control Flow Guard on, as Vanguard needs. Each nibble is one
        // mitigation: 1 enables it, 2 disables it, 0 leaves it at Windows' default.
        //
        // Windows uses its own defaults when the value is missing, and Windows Security's "Reset all to
        // default" writes those same defaults out as an explicit all-enabled mask instead of deleting
        // the value. Both are Windows' default, so that mask is the target's AbsentMeans and either one
        // reads as "Windows default"; a mask the user built by hand still reads as Custom.
        var mitigations = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "MitigationOptions"),
            AbsentMeans: new RegistryValue(RegistryValueKind.Binary, Convert.FromHexString("111111000001000000000000000000000000000000000000")));

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
        // They do not always stay off: with tamper protection on, Defender rewrote all four task
        // files back to Enabled nine minutes after an apply on DESKTOP-LLLM7SU, with Akari-Dash
        // closed. Nothing here can stop that (it needs safe boot and TrustedInstaller, which is out
        // of scope), so the description says so and Live State keeps telling the truth: it reads On
        // again while Original Values still records the Off apply, which Undo then clears.
        TweakTarget[] targets =
        [
            .. new[] { "Windows Defender Cache Maintenance", "Windows Defender Cleanup", "Windows Defender Scheduled Scan", "Windows Defender Verification" }
                .Select(task => new TweakTarget(new ScheduledTaskLocation($@"\Microsoft\Windows\Windows Defender\{task}"))),
        ];

        return SecurityTweak("security.defender-tasks", "Defender scheduled tasks",
            "Defender's background scan, cache upkeep, cleanup and update check, which can start while you play. Off stops them; real-time protection still scans files as they are opened. If tamper protection is on, Defender turns these tasks back on by itself after a while, and this Tweak reads On again.",
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

    private static DeclaredTweak DefenderOptimizeTweak()
    {
        // AkariOS-Ultimate's 'defender' ("Defender optimize"), Tweaks/Windows.ps1:5705 — the whole Tweak,
        // not only the part of it that needs safe boot. One toggle that covers the rows beneath it, each
        // of which also holds a value this one writes: two Tweaks may share a location on purpose, and an
        // Original Value belongs to the location rather than to the Tweak, so the machine's own value
        // for one is what Undo puts back, and it is the same value every other row here puts back.
        //
        // Its Optimize is AkariOS's apply list and its Windows default is the same list AkariOS's -Revert
        // writes, value for value, except virtualization based security, which that Revert leaves off and
        // this one turns back on, because an Undo of this Tweak is an Undo of all of it.
        //
        // Real-time protection stays ON: AkariOS's Optimize sets DisableRealtimeMonitoring to 0, not
        // 1, because Windows turns it back on by itself. Smart App Control is the one part that
        // cannot be undone — Windows marks it off for good the first time it is turned off, and
        // nothing, this Undo included, brings it back without a reinstall — so the description says so.
        //
        // A missing value counts as the Windows default, which is what the Undo puts back: these keys
        // are normally there, and "not set" most usefully reads as "nothing has been optimized here".
        //
        // What does not need safe mode is written by Akari-Dash itself, in the session that asked for it,
        // and only the keys Windows protects are left to the pass: a current-user value is in that
        // session's own hive, and safe mode may sign in as a different account, which would put it on the
        // wrong profile.
        const string defender = @"SOFTWARE\Microsoft\Windows Defender";
        const string smartLocker = @"SYSTEM\CurrentControlSet\Control\AppID\Configuration\SMARTLOCKER";
        const string ciPolicy = @"SYSTEM\CurrentControlSet\Control\CI\Policy";
        const string securityCenter = @"SOFTWARE\Microsoft\Windows Defender Security Center";
        const string hypervisorEnforced = @"SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
        const string deviceGuard = @"SYSTEM\ControlSet001\Control\DeviceGuard";
        const string kernel = @"SYSTEM\ControlSet001\Control\Session Manager\kernel";
        const string sharedAccess = @"SYSTEM\ControlSet001\Services\SharedAccess";

        // The mask that turns every system-wide mitigation off but Control Flow Guard on, as Vanguard
        // needs, and the mask Windows writes out for itself as its default.
        var mitigationsOff = new RegistryValue(RegistryValueKind.Binary, Convert.FromHexString("222222000002000000020000000000000000000000000000"));
        var mitigationsDefault = new RegistryValue(RegistryValueKind.Binary, Convert.FromHexString("111111000001000000000000000000000000000000000000"));

        TweakTarget Target(string key, string name, RegistryValue windowsDefault) =>
            new(new RegistryLocation(RegistryHive.LocalMachine, key, name), AbsentMeans: windowsDefault);

        TweakTarget[] firewallProfiles =
        [
            .. new[] { "DomainProfile", "PublicProfile", "StandardProfile" }
                .Select(profile => Target($@"{sharedAccess}\Parameters\FirewallPolicy\{profile}", "DisableNotifications", RegistryValue.DWord(0)))
        ];

        // In the session's own hive, so written there rather than in safe mode, which may be another.
        TweakTarget[] accountProtection =
        [
            .. new[] { "DisableNotifications", "DisableDynamiclockNotifications", "DisableWindowsHelloNotifications" }
                .Select(name => new TweakTarget(
                    new RegistryLocation(RegistryHive.CurrentUser, $@"{securityCenter}\Account protection", name),
                    AbsentMeans: RegistryValue.DWord(0),
                    WrittenInOwnSession: true))
        ];
        TweakTarget[] currentUser =
        [
            new(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenEnabled", ""), AbsentMeans: RegistryValue.DWord(1), WrittenInOwnSession: true),
            new(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenPuaEnabled", ""), AbsentMeans: RegistryValue.DWord(1), WrittenInOwnSession: true),
            new(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost", "EnableWebContentEvaluation"), AbsentMeans: RegistryValue.DWord(1), WrittenInOwnSession: true),
        ];
        TweakTarget[] vbsBootSettings =
        [
            new(new BootSettingLocation("hypervisorlaunchtype"), AbsentMeans: new BootSettingValue("Auto")),
            new(new BootSettingLocation("isolatedcontext")),
            new(new BootSettingLocation("allowedinmemorysettings")),
        ];
        TweakTarget[] defenderTasks =
        [
            .. new[] { "Windows Defender Cache Maintenance", "Windows Defender Cleanup", "Windows Defender Scheduled Scan", "Windows Defender Verification" }
                .Select(task => new TweakTarget(new ScheduledTaskLocation($@"\Microsoft\Windows\Windows Defender\{task}"), WrittenInOwnSession: true))
        ];

        TweakTarget[] targets =
        [
            // Defender's own settings, and Smart App Control.
            Target($@"{defender}\Real-Time Protection", "DisableRealtimeMonitoring", RegistryValue.DWord(0)),
            Target($@"{defender}\Real-Time Protection", "DisableAsyncScanOnOpen", RegistryValue.DWord(0)),
            Target($@"{defender}\Spynet", "SpyNetReporting", RegistryValue.DWord(2)),
            Target($@"{defender}\Spynet", "SubmitSamplesConsent", RegistryValue.DWord(1)),
            Target($@"{defender}\Features", "TamperProtection", RegistryValue.DWord(5)),
            Target($@"{defender}\Windows Defender Exploit Guard\Controlled Folder Access", "EnableControlledFolderAccess", RegistryValue.DWord(1)),
            Target(defender, "PUAProtection", RegistryValue.DWord(1)),
            Target(defender, "VerifiedAndReputableTrustModeEnabled", RegistryValue.DWord(1)),
            Target(defender, "SmartLockerMode", RegistryValue.DWord(1)),
            Target(smartLocker, "START_PENDING", RegistryValue.DWord(4)),
            Target(smartLocker, "ENABLED", SmartAppControl(enabled: true)),
            Target(ciPolicy, "VerifiedAndReputablePolicyState", RegistryValue.DWord(1)),

            // Windows Security's own notifications and the firewall's quiet profiles.
            Target($@"{securityCenter}\Notifications", "DisableEnhancedNotifications", RegistryValue.DWord(0)),
            Target($@"{securityCenter}\Virus and threat protection", "NoActionNotificationDisabled", RegistryValue.DWord(0)),
            Target($@"{securityCenter}\Virus and threat protection", "SummaryNotificationDisabled", RegistryValue.DWord(0)),
            Target($@"{securityCenter}\Virus and threat protection", "FilesBlockedNotificationDisabled", RegistryValue.DWord(0)),
            Target($@"{sharedAccess}\Epoch", "Epoch", RegistryValue.DWord(1228)),
            .. firewallProfiles,

            // SmartScreen for apps and files.
            Target(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", RegistryValue.String("Warn")),

            // The system-wide exploit mitigations, memory integrity, virtualization based security and
            // its boot settings, LSA protection and the vulnerable driver blocklist.
            Target(kernel, "MitigationOptions", mitigationsDefault),
            Target(hypervisorEnforced, "Enabled", RegistryValue.DWord(0)),
            Target(hypervisorEnforced, "WasEnabledBy", null),
            Target(hypervisorEnforced, "ChangedInBootCycle", null),
            Target(deviceGuard, "EnableVirtualizationBasedSecurity", RegistryValue.DWord(1)),
            .. vbsBootSettings,
            Target(@"SYSTEM\CurrentControlSet\Control\Lsa", "RunAsPPL", RegistryValue.DWord(2)),
            Target(@"SYSTEM\ControlSet001\Control\CI\Config", "VulnerableDriverBlocklistEnable", RegistryValue.DWord(1)),

            .. accountProtection,
            .. currentUser,
            .. defenderTasks,
        ];

        return new DeclaredTweak("security.defender-optimize", "Defender optimize",
            "AkariOS-Ultimate's one-press Defender optimize, the whole of it in one toggle: cloud protection, sample submission, tamper protection, Controlled Folder Access, potentially unwanted app blocking, Smart App Control and phishing protection off, files no longer scanned as they open, and Windows Security's and the firewall's own notifications, SmartScreen for apps and for Edge and Store apps, the system-wide exploit mitigations, memory integrity, virtualization-based security, LSA protection, the vulnerable driver blocklist and Defender's own scheduled tasks switched off as well. Real-time protection stays on. Windows only lets Defender's own keys be written in safe boot, so applying or undoing it restarts the PC into safe mode and back, and the rows it covers are put in the state this one chooses. Smart App Control cannot be turned back on afterwards by anything, this Undo included, without reinstalling Windows.",
            Category.Security, "Microsoft Defender", targets,
            [Optimize(), WindowsDefault()],
            Activation: Activation.InSafeMode);

        TweakOption Optimize()
        {
            var values = new Dictionary<TweakTarget, MachineValue?>
            {
                // Defender's own settings.
                [targets[0]] = RegistryValue.DWord(0),
                [targets[1]] = RegistryValue.DWord(1),
                [targets[2]] = RegistryValue.DWord(0),
                [targets[3]] = RegistryValue.DWord(0),
                [targets[4]] = RegistryValue.DWord(4),
                [targets[5]] = RegistryValue.DWord(0),
                [targets[6]] = RegistryValue.DWord(0),
                [targets[7]] = RegistryValue.DWord(0),
                [targets[8]] = RegistryValue.DWord(0),
                [targets[9]] = RegistryValue.DWord(0),
                [targets[10]] = SmartAppControl(enabled: false),
                [targets[11]] = RegistryValue.DWord(0),

                // The notifications, the firewall's quiet profiles and SmartScreen off for apps.
                [targets[12]] = RegistryValue.DWord(1),
                [targets[13]] = RegistryValue.DWord(1),
                [targets[14]] = RegistryValue.DWord(1),
                [targets[15]] = RegistryValue.DWord(1),
                [targets[16]] = RegistryValue.DWord(1231),
                [targets[17]] = RegistryValue.DWord(1),
                [targets[18]] = RegistryValue.DWord(1),
                [targets[19]] = RegistryValue.DWord(1),
                [targets[20]] = RegistryValue.String("Off"),

                // Every system-wide mitigation off, memory integrity off, virtualization based security
                // off, LSA protection off, the vulnerable driver blocklist off, and the account
                // protection reminders and Edge's and Store apps' SmartScreen off too.
                [targets[21]] = mitigationsOff,
                [targets[22]] = RegistryValue.DWord(0),
                [targets[23]] = null,
                [targets[24]] = null,
                [targets[25]] = null,
                [targets[26]] = null,
                [targets[27]] = null,
                [targets[28]] = null,
                [targets[29]] = RegistryValue.DWord(0),
                [targets[30]] = RegistryValue.DWord(0),
                [targets[31]] = RegistryValue.DWord(1),
                [targets[32]] = RegistryValue.DWord(1),
                [targets[33]] = RegistryValue.DWord(1),
                [targets[34]] = RegistryValue.DWord(0),
                [targets[35]] = RegistryValue.DWord(0),
                [targets[36]] = RegistryValue.DWord(0),
            };

            foreach (var task in defenderTasks)
            {
                values[task] = TaskEnabledValue.Off;
            }

            return new("optimize", "Optimize", values);
        }

        // Windows default is what a missing value means, except where Windows writes the value out
        // itself rather than leaving it absent: memory integrity on, with its bookkeeping saying the user
        // turned it on (a missing Enabled counts as off, but Windows' default is it on), and Defender's
        // four tasks, which Windows ships running.
        TweakOption WindowsDefault()
        {
            var values = new Dictionary<TweakTarget, MachineValue?>(AbsentMeansOption(targets, "windows-default", "Windows default").Values);
            values[targets[22]] = RegistryValue.DWord(1);
            values[targets[23]] = RegistryValue.DWord(2);
            foreach (var task in defenderTasks)
            {
                values[task] = TaskEnabledValue.On;
            }

            return new("windows-default", "Windows default", values);
        }

        // Smart App Control keeps its flag in the first byte of an eight-byte value.
        static RegistryValue SmartAppControl(bool enabled) =>
            new(RegistryValueKind.Binary, new byte[] { enabled ? (byte)4 : (byte)0, 0, 0, 0, 0, 0, 0, 0 });
    }

    private static DeclaredTweak DisableDefender()
    {
        // AkariOS-Ultimate's Advanced 'Defender' (Tweaks/Advanced.ps1:3-435): its disable list as Off
        // and its enable list as On, one row. It is the second safe-mode Tweak, beside Defender
        // optimize, and it reuses most of that Tweak's locations and AbsentMeans on purpose: an
        // Original Value belongs to the location, so the two rows share it and put the same value back.
        //
        // Off is the disable list value for value; On is the enable list with three settled calls the
        // design records: On restores virtualization-based security and its hypervisor boot setting
        // (AkariOS's enable list leaves VBS off, which would leave Hyper-V, WSL2 and Windows Sandbox
        // broken after the user chose to enable), On leaves the exploit mitigation mask absent (that is
        // Windows' own default, the state AkariOS's all-enabled mask stands for), and the eleven
        // services and drivers are registry Start values rather than ServiceLocation targets, because
        // that is the only write that reaches all of them.
        //
        // Two parts do not come back and the description says so: Smart App Control, which Windows
        // marks off for good, and the Windows-Defender-Default-Definitions DISM feature the companion
        // disables best-effort.
        const string defender = @"SOFTWARE\Microsoft\Windows Defender";
        const string securityCenter = @"SOFTWARE\Microsoft\Windows Defender Security Center";
        const string smartLocker = @"SYSTEM\CurrentControlSet\Control\AppID\Configuration\SMARTLOCKER";
        const string ciPolicy = @"SYSTEM\CurrentControlSet\Control\CI\Policy";
        const string hypervisorEnforced = @"SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
        const string deviceGuard = @"SYSTEM\ControlSet001\Control\DeviceGuard";
        const string kernel = @"SYSTEM\ControlSet001\Control\Session Manager\kernel";
        const string sharedAccess = @"SYSTEM\ControlSet001\Services\SharedAccess";
        const string ciConfig = @"SYSTEM\ControlSet001\Control\CI\Config";
        const string lsa = @"SYSTEM\CurrentControlSet\Control\Lsa";
        const string services = @"SYSTEM\CurrentControlSet\Services";
        const string accountProtectionKey = @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection";
        const string eppHandler = "{09A47860-11B0-4DA5-AFA5-26D86198A780}";

        // Advanced.ps1:104's mask: every system-wide mitigation off but Control Flow Guard on, which
        // Vanguard needs. Windows writes that mask's all-enabled twin out as its own default, which is
        // this target's AbsentMeans.
        var mitigationsOff = new RegistryValue(RegistryValueKind.Binary, Convert.FromHexString("222222000001000000000000000000000000000000000000"));
        var mitigationsDefault = new RegistryValue(RegistryValueKind.Binary, Convert.FromHexString("111111000001000000000000000000000000000000000000"));
        var tray = new RegistryValue(RegistryValueKind.ExpandString, @"%windir%\system32\SecurityHealthSystray.exe");

        // A machine-wide value: a missing one counts as Windows' own, which is what On puts back.
        TweakTarget Target(string key, string name, RegistryValue windowsDefault) =>
            new(new RegistryLocation(RegistryHive.LocalMachine, key, name), AbsentMeans: windowsDefault);

        // A current-user value, written in the session that asked: safe mode may sign in as someone else.
        TweakTarget Owned(RegistryHive hive, string key, string name, RegistryValue windowsDefault) =>
            new(new RegistryLocation(hive, key, name), AbsentMeans: windowsDefault, WrittenInOwnSession: true);

        // A service or driver as its registry Start value.
        TweakTarget Start(string service, int windowsDefault) =>
            new(new RegistryLocation(RegistryHive.LocalMachine, $@"{services}\{service}", "Start"), AbsentMeans: RegistryValue.DWord(windowsDefault));

        // A right-click menu entry: the default value of the EPP key under Directory, Drive and *.
        TweakTarget Epp(string scope) =>
            new(new RegistryLocation(RegistryHive.LocalMachine, $@"SOFTWARE\Classes\{scope}\shellex\ContextMenuHandlers\EPP", ""),
                AbsentMeans: RegistryValue.String(eppHandler), WrittenInOwnSession: true);

        // Defender's own settings: real-time and cloud protection, tamper protection, Controlled Folder
        // Access, unwanted app blocking, trusted mode and Smart Locker mode.
        var realtime = Target($@"{defender}\Real-Time Protection", "DisableRealtimeMonitoring", RegistryValue.DWord(0));
        var asyncScan = Target($@"{defender}\Real-Time Protection", "DisableAsyncScanOnOpen", RegistryValue.DWord(0));
        var spyNet = Target($@"{defender}\Spynet", "SpyNetReporting", RegistryValue.DWord(2));
        var samples = Target($@"{defender}\Spynet", "SubmitSamplesConsent", RegistryValue.DWord(1));
        var tamper = Target($@"{defender}\Features", "TamperProtection", RegistryValue.DWord(5));
        var folderAccess = Target($@"{defender}\Windows Defender Exploit Guard\Controlled Folder Access", "EnableControlledFolderAccess", RegistryValue.DWord(1));
        var pua = Target(defender, "PUAProtection", RegistryValue.DWord(1));
        var trustMode = Target(defender, "VerifiedAndReputableTrustModeEnabled", RegistryValue.DWord(1));
        var smartLockerMode = Target(defender, "SmartLockerMode", RegistryValue.DWord(1));
        TweakTarget[] defenderSettings = [realtime, asyncScan, spyNet, samples, tamper, folderAccess, pua, trustMode, smartLockerMode];

        // Smart App Control, which Windows marks off for good.
        var startPending = Target(smartLocker, "START_PENDING", RegistryValue.DWord(4));
        var smartLockerEnabled = Target(smartLocker, "ENABLED", SmartAppControl(enabled: true));
        var policyState = Target(ciPolicy, "VerifiedAndReputablePolicyState", RegistryValue.DWord(1));
        TweakTarget[] smartAppControl = [startPending, smartLockerEnabled, policyState];

        // Windows Security's notifications, the firewall's Epoch and quiet profiles, and SmartScreen.
        var enhancedNotifications = Target($@"{securityCenter}\Notifications", "DisableEnhancedNotifications", RegistryValue.DWord(0));
        var noActionNotification = Target($@"{securityCenter}\Virus and threat protection", "NoActionNotificationDisabled", RegistryValue.DWord(0));
        var summaryNotification = Target($@"{securityCenter}\Virus and threat protection", "SummaryNotificationDisabled", RegistryValue.DWord(0));
        var filesBlockedNotification = Target($@"{securityCenter}\Virus and threat protection", "FilesBlockedNotificationDisabled", RegistryValue.DWord(0));
        var epoch = Target($@"{sharedAccess}\Epoch", "Epoch", RegistryValue.DWord(1228));
        var domainProfile = Target($@"{sharedAccess}\Parameters\FirewallPolicy\DomainProfile", "DisableNotifications", RegistryValue.DWord(0));
        var publicProfile = Target($@"{sharedAccess}\Parameters\FirewallPolicy\PublicProfile", "DisableNotifications", RegistryValue.DWord(0));
        var standardProfile = Target($@"{sharedAccess}\Parameters\FirewallPolicy\StandardProfile", "DisableNotifications", RegistryValue.DWord(0));
        var smartScreenEnabled = Target(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", RegistryValue.String("Warn"));
        TweakTarget[] notifications =
        [
            enhancedNotifications, noActionNotification, summaryNotification, filesBlockedNotification,
            epoch, domainProfile, publicProfile, standardProfile, smartScreenEnabled,
        ];

        // Edge's and Store apps' SmartScreen (current-user), exploit protection, memory integrity, VBS
        // with its hypervisor boot settings, LSA protection and the vulnerable driver blocklist.
        var edgeSmartScreen = Owned(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenEnabled", "", RegistryValue.DWord(1));
        var edgePua = Owned(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenPuaEnabled", "", RegistryValue.DWord(1));
        var appHost = Owned(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost", "EnableWebContentEvaluation", RegistryValue.DWord(1));
        var mitigations = new TweakTarget(new RegistryLocation(RegistryHive.LocalMachine, kernel, "MitigationOptions"), AbsentMeans: mitigationsDefault);
        // Memory integrity's own values keep the AbsentMeans the memory integrity row gives them: a
        // missing Enabled counts as off, and the two bookkeeping values have none.
        var hvciEnabled = Target(hypervisorEnforced, "Enabled", RegistryValue.DWord(0));
        var hvciWasEnabledBy = new TweakTarget(new RegistryLocation(RegistryHive.LocalMachine, hypervisorEnforced, "WasEnabledBy"));
        var hvciChanged = new TweakTarget(new RegistryLocation(RegistryHive.LocalMachine, hypervisorEnforced, "ChangedInBootCycle"));
        var vbs = Target(deviceGuard, "EnableVirtualizationBasedSecurity", RegistryValue.DWord(1));
        var hypervisorLaunch = new TweakTarget(new BootSettingLocation("hypervisorlaunchtype"), AbsentMeans: new BootSettingValue("Auto"));
        var isolatedContext = new TweakTarget(new BootSettingLocation("isolatedcontext"));
        var allowedInMemory = new TweakTarget(new BootSettingLocation("allowedinmemorysettings"));
        var runAsPpl = Target(lsa, "RunAsPPL", RegistryValue.DWord(2));
        var blocklist = Target(ciConfig, "VulnerableDriverBlocklistEnable", RegistryValue.DWord(1));
        TweakTarget[] coreIsolation =
        [
            edgeSmartScreen, edgePua, appHost, mitigations, hvciEnabled, hvciWasEnabledBy, hvciChanged,
            vbs, hypervisorLaunch, isolatedContext, allowedInMemory, runAsPpl, blocklist,
        ];

        // Defender's eight services and three drivers, as registry Start values.
        var wdNisSvc = Start("WdNisSvc", 3);
        var winDefend = Start("WinDefend", 2);
        var mdCoreSvc = Start("MDCoreSvc", 2);
        var wscsvc = Start("wscsvc", 2);
        var webthreatDefSvc = Start("webthreatdefsvc", 3);
        var webthreatDefUserSvc = Start("webthreatdefusersvc", 2);
        var sense = Start("Sense", 3);
        var securityHealthService = Start("SecurityHealthService", 2);
        var wdBoot = Start("WdBoot", 0);
        var wdFilter = Start("WdFilter", 0);
        var wdNisDrv = Start("WdNisDrv", 3);
        TweakTarget[] servicesAndDrivers =
        [
            wdNisSvc, winDefend, mdCoreSvc, wscsvc, webthreatDefSvc, webthreatDefUserSvc,
            sense, securityHealthService, wdBoot, wdFilter, wdNisDrv,
        ];

        // What Akari-Dash writes in the session that asked: the right-click menu entries, the tray icon,
        // the four Defender tasks and the account protection reminders. Safe mode may be another login,
        // so these do not go to the pass.
        var directoryEpp = Epp("Directory");
        var driveEpp = Epp("Drive");
        var starEpp = Epp("*");
        var securityHealth = Owned(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "SecurityHealth", tray);
        TweakTarget[] ownSessionStart = [directoryEpp, driveEpp, starEpp, securityHealth];
        TweakTarget[] defenderTasks =
        [
            .. new[] { "Windows Defender Cache Maintenance", "Windows Defender Cleanup", "Windows Defender Scheduled Scan", "Windows Defender Verification" }
                .Select(task => new TweakTarget(new ScheduledTaskLocation($@"\Microsoft\Windows\Windows Defender\{task}"), WrittenInOwnSession: true))
        ];
        TweakTarget[] accountProtection =
        [
            .. new[] { "DisableNotifications", "DisableDynamiclockNotifications", "DisableWindowsHelloNotifications" }
                .Select(name => Owned(RegistryHive.CurrentUser, accountProtectionKey, name, RegistryValue.DWord(0)))
        ];

        TweakTarget[] targets =
        [
            .. defenderSettings,
            .. smartAppControl,
            .. notifications,
            .. coreIsolation,
            .. servicesAndDrivers,
            .. ownSessionStart,
            .. defenderTasks,
            .. accountProtection,
        ];

        return new DeclaredTweak("security.disable-defender", "Disable Defender",
            "AkariOS-Ultimate's Advanced Defender switch, the whole of it: real-time, dev drive and cloud protection off, tamper protection off, Controlled Folder Access off, and Defender's own eight services and three drivers disabled, so Windows Defender and Windows Security's tray and right-click entries are gone from the PC. It also takes with it everything the rows above cover — SmartScreen for apps, Edge and Store apps, the system-wide exploit mitigations, memory integrity, virtualization-based security, LSA protection, the vulnerable driver blocklist, Windows Security's and the firewall's notifications and Defender's own scheduled tasks — and puts all of that back with On. Windows only lets Defender's keys be written in safe boot, so applying or undoing it restarts the PC into safe mode and back. Two pieces do not come back: disabling Windows-Defender-Default-Definitions through DISM cannot be undone by anything, and Smart App Control cannot be turned back on without reinstalling Windows, On's writes included. With Defender's services off, Windows Security can no longer report on this PC and no antivirus is running; undoing brings both back.",
            Category.Security, "Microsoft Defender", targets,
            [Off(), On()],
            Activation: Activation.InSafeMode);

        TweakOption Off()
        {
            var values = new Dictionary<TweakTarget, MachineValue?>
            {
                // Defender's own settings, all off.
                [realtime] = RegistryValue.DWord(1),
                [asyncScan] = RegistryValue.DWord(1),
                [spyNet] = RegistryValue.DWord(0),
                [samples] = RegistryValue.DWord(0),
                [tamper] = RegistryValue.DWord(4),
                [folderAccess] = RegistryValue.DWord(0),
                [pua] = RegistryValue.DWord(0),
                [trustMode] = RegistryValue.DWord(0),
                [smartLockerMode] = RegistryValue.DWord(0),

                // Smart App Control off.
                [startPending] = RegistryValue.DWord(0),
                [smartLockerEnabled] = SmartAppControl(enabled: false),
                [policyState] = RegistryValue.DWord(0),

                // Windows Security's notifications, the firewall's and SmartScreen, off.
                [enhancedNotifications] = RegistryValue.DWord(1),
                [noActionNotification] = RegistryValue.DWord(1),
                [summaryNotification] = RegistryValue.DWord(1),
                [filesBlockedNotification] = RegistryValue.DWord(1),
                [epoch] = RegistryValue.DWord(1231),
                [domainProfile] = RegistryValue.DWord(1),
                [publicProfile] = RegistryValue.DWord(1),
                [standardProfile] = RegistryValue.DWord(1),
                [smartScreenEnabled] = RegistryValue.String("Off"),

                // The exploit mitigations off but Control Flow Guard on, memory integrity off with its
                // bookkeeping deleted, VBS and its hypervisor boot settings deleted, LSA protection off
                // and the driver blocklist off. Edge's and Store apps' SmartScreen off too.
                [edgeSmartScreen] = RegistryValue.DWord(0),
                [edgePua] = RegistryValue.DWord(0),
                [appHost] = RegistryValue.DWord(0),
                [mitigations] = mitigationsOff,
                [hvciEnabled] = RegistryValue.DWord(0),
                [hvciWasEnabledBy] = null,
                [hvciChanged] = null,
                [vbs] = null,
                [hypervisorLaunch] = null,
                [isolatedContext] = null,
                [allowedInMemory] = null,
                [runAsPpl] = RegistryValue.DWord(0),
                [blocklist] = RegistryValue.DWord(0),

                // Every service and driver Disabled.
                [wdNisSvc] = RegistryValue.DWord(4),
                [winDefend] = RegistryValue.DWord(4),
                [mdCoreSvc] = RegistryValue.DWord(4),
                [wscsvc] = RegistryValue.DWord(4),
                [webthreatDefSvc] = RegistryValue.DWord(4),
                [webthreatDefUserSvc] = RegistryValue.DWord(4),
                [sense] = RegistryValue.DWord(4),
                [securityHealthService] = RegistryValue.DWord(4),
                [wdBoot] = RegistryValue.DWord(4),
                [wdFilter] = RegistryValue.DWord(4),
                [wdNisDrv] = RegistryValue.DWord(4),

                // The tray icon and the right-click menu entries deleted, and the account protection
                // reminders on.
                [directoryEpp] = null,
                [driveEpp] = null,
                [starEpp] = null,
                [securityHealth] = null,
                [accountProtection[0]] = RegistryValue.DWord(1),
                [accountProtection[1]] = RegistryValue.DWord(1),
                [accountProtection[2]] = RegistryValue.DWord(1),
            };

            foreach (var task in defenderTasks)
            {
                values[task] = TaskEnabledValue.Off;
            }

            return new("off", "Off", values);
        }

        TweakOption On()
        {
            var values = new Dictionary<TweakTarget, MachineValue?>
            {
                // Defender's own settings, all back on.
                [realtime] = RegistryValue.DWord(0),
                [asyncScan] = RegistryValue.DWord(0),
                [spyNet] = RegistryValue.DWord(2),
                [samples] = RegistryValue.DWord(1),
                [tamper] = RegistryValue.DWord(5),
                [folderAccess] = RegistryValue.DWord(1),
                // The enable list writes PUAProtection 2 and then 1; 1 is what it settles on.
                [pua] = RegistryValue.DWord(1),
                [trustMode] = RegistryValue.DWord(1),
                [smartLockerMode] = RegistryValue.DWord(1),

                // Smart App Control's values back, though Windows will not act on them.
                [startPending] = RegistryValue.DWord(4),
                [smartLockerEnabled] = SmartAppControl(enabled: true),
                [policyState] = RegistryValue.DWord(1),

                [enhancedNotifications] = RegistryValue.DWord(0),
                [noActionNotification] = RegistryValue.DWord(0),
                [summaryNotification] = RegistryValue.DWord(0),
                [filesBlockedNotification] = RegistryValue.DWord(0),
                [epoch] = RegistryValue.DWord(1228),
                [domainProfile] = RegistryValue.DWord(0),
                [publicProfile] = RegistryValue.DWord(0),
                [standardProfile] = RegistryValue.DWord(0),
                [smartScreenEnabled] = RegistryValue.String("Warn"),

                [edgeSmartScreen] = RegistryValue.DWord(1),
                [edgePua] = RegistryValue.DWord(1),
                [appHost] = RegistryValue.DWord(1),
                // Windows' own default, which is the value being absent.
                [mitigations] = null,
                [hvciEnabled] = RegistryValue.DWord(1),
                [hvciWasEnabledBy] = RegistryValue.DWord(2),
                [hvciChanged] = null,
                [vbs] = RegistryValue.DWord(1),
                [hypervisorLaunch] = new BootSettingValue("Auto"),
                [isolatedContext] = null,
                [allowedInMemory] = null,
                [runAsPpl] = RegistryValue.DWord(2),
                [blocklist] = RegistryValue.DWord(1),

                // Each service and driver back at the Start value Windows ships it at.
                [wdNisSvc] = RegistryValue.DWord(3),
                [winDefend] = RegistryValue.DWord(2),
                [mdCoreSvc] = RegistryValue.DWord(2),
                [wscsvc] = RegistryValue.DWord(2),
                [webthreatDefSvc] = RegistryValue.DWord(3),
                [webthreatDefUserSvc] = RegistryValue.DWord(2),
                [sense] = RegistryValue.DWord(3),
                [securityHealthService] = RegistryValue.DWord(2),
                [wdBoot] = RegistryValue.DWord(0),
                [wdFilter] = RegistryValue.DWord(0),
                [wdNisDrv] = RegistryValue.DWord(3),

                // The tray icon and the menu entries back, and the reminders off.
                [directoryEpp] = RegistryValue.String(eppHandler),
                [driveEpp] = RegistryValue.String(eppHandler),
                [starEpp] = RegistryValue.String(eppHandler),
                [securityHealth] = tray,
                [accountProtection[0]] = RegistryValue.DWord(0),
                [accountProtection[1]] = RegistryValue.DWord(0),
                [accountProtection[2]] = RegistryValue.DWord(0),
            };

            foreach (var task in defenderTasks)
            {
                values[task] = TaskEnabledValue.On;
            }

            return new("on", "On", values);
        }

        // Smart App Control keeps its flag in the first byte of an eight-byte value.
        static RegistryValue SmartAppControl(bool enabled) =>
            new(RegistryValueKind.Binary, new byte[] { enabled ? (byte)4 : (byte)0, 0, 0, 0, 0, 0, 0, 0 });
    }
}
