using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

// Windows Update Tweaks: when updates arrive, what comes with them and how much Windows says about
// it. Written from Winhance as of its last MIT commit (4dc441a0; see THIRD-PARTY-NOTICES.md), with
// the deferral values taken from Microsoft's Windows Update for Business policy documentation —
// see UpdateDeferral below for why. ADR-0004 puts update policy in scope: it is ordinary registry
// data that reads back and undoes like any other Tweak.
//
// This is the page where a wrong Option does real damage, so every description says what stops
// happening, not just what the row is called. Nothing here has a Recommended Option except Delivery
// Optimization, the one row that is purely about bandwidth: "apply all recommended" must never be
// the reason a PC quietly stopped getting security fixes.
//
// Winhance writes its AU and WindowsUpdate policy values for the machine and the user both, and so
// does this page. The Windows Update Agent reads the machine copy; the user copy is what Winhance
// puts beside it, and keeping both means Undo puts both back.
//
// Left out of Winhance's own update page:
//
//   UseWUServer. Winhance writes 0 to say "do not use a WSUS server". On a home PC the value is
//   already absent and writing it changes nothing; on a managed PC it fights whatever put the PC on
//   a WSUS in the first place. Neither is a decision this page should be making.
//
//   AutoInstallMinorUpdates and AlwaysAutoRebootAtScheduledTime, which Winhance only touches inside
//   its paused mode. Restart behaviour is one decision here, update.restart-while-signed-in.
//
//   Winhance's "Paused for a long time" mode, which writes a pause window from 2025-01-01 to
//   2051-12-31 plus FlightSettingsMaxPauseDays. Those dates are baked into the build — its start is
//   already in the past — the pause window belongs to Settings' own Pause updates page, which
//   rewrites it, and FlightSettingsMaxPauseDays is undocumented. update.automatic-updates' "Never
//   check on its own" reaches the same end through the documented policy, and undoes cleanly.
public static partial class TweakCatalog
{
    private const string AutoUpdatePolicies = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
    private const string WindowsUpdatePolicies = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
    private const string UpdateUxSettings = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
    private const string DeliveryOptimizationPolicies = @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
    private const string StorePolicies = @"SOFTWARE\Policies\Microsoft\WindowsStore";

    private static IEnumerable<DeclaredTweak> UpdateTweaks()
    {
        var automatic = AutomaticUpdates();
        var featureUpdates = UpdateDeferral(
            "update.feature-updates", "Hold back feature updates", "hold-back-a-year", "Hold back for a year",
            "Keeps this PC on the Windows version it has for a year after Microsoft releases the next one. Monthly security updates still arrive — only the version upgrade waits. Pro, Enterprise and Education honour this policy; Windows Home ignores it and upgrades on Microsoft's own schedule.",
            [("BranchReadinessLevel", 20), ("DeferFeatureUpdates", 1), ("DeferFeatureUpdatesPeriodInDays", 365)]);
        var qualityUpdates = UpdateDeferral(
            "update.quality-updates", "Hold back monthly updates", "hold-back-a-week", "Hold back for a week",
            "Holds each month's quality update — the security and reliability fixes — back for a week after Microsoft releases it, which is long enough for a bad one to be pulled. It is also a week this PC spends unpatched against everything that update fixes. Pro, Enterprise and Education only.",
            [("DeferQualityUpdates", 1), ("DeferQualityUpdatesPeriodInDays", 7)]);

        return
        [
            UpdatePolicyPreset(automatic, featureUpdates, qualityUpdates),
            automatic,
            featureUpdates,
            qualityUpdates,
            RestartWhileSignedIn(),
            ShutdownOption(),

            UxSettingOnOff("update.latest-updates", "Get the latest updates as soon as they're available",
                "Puts this PC at the front of the queue for non-security fixes and new features, ahead of the general rollout. On, those changes land here first, the bad ones included. Settings > Windows Update holds the same switch.",
                "IsContinuousInnovationOptedIn"),
            UxSettingOnOff("update.other-microsoft-products", "Updates for other Microsoft products",
                "Lets Windows Update fetch updates for other Microsoft software too, Office among it. Off leaves each of those apps to update itself, and anything without an updater of its own stops getting fixes.",
                "AllowMUUpdateService"),
            UxSettingOnOff("update.restart-asap", "Restart as soon as possible to finish updating",
                "Lets Windows restart this PC to finish an update at the first chance it gets, active hours included. On, a restart can arrive in the middle of whatever you are doing; off, it waits for the quiet hours Windows picks.",
                "IsExpedited"),
            UpdateNotifications(),
            UxSettingOnOff("update.restart-notification", "Notify me when a restart is needed",
                "The notice Windows shows once an update is installed and only the restart is left. Off, nothing is said, and the PC sits with the update half-applied until you happen to restart it.",
                "RestartNotificationsAllowed2"),
            UxSettingOnOff("update.metered-connection", "Download updates over metered connections",
                "Lets Windows download updates over a connection marked as metered, such as a phone hotspot. On can spend a data allowance without asking; off means a PC that only ever sees a metered connection downloads no updates at all.",
                "AllowAutoWindowsUpdateDownloadOverMeteredNetwork"),

            DriverUpdates(),

            DeliveryOptimization(),
            StoreAppUpdates(),
        ];
    }

    /// <summary>
    /// Winhance's update policy switch, as a preset over the three Tweaks below it: the same shape as
    /// privacy.suggestions-preset. It owns no value of its own, so a mix reads as Custom until a
    /// choice is made here, and each row underneath still answers for itself.
    /// </summary>
    private static DeclaredTweak UpdatePolicyPreset(params DeclaredTweak[] covered)
    {
        TweakTarget[] targets = [.. covered.SelectMany(tweak => tweak.Targets)];

        return new DeclaredTweak(
            Id: "update.policy",
            Title: "Update policy",
            Description: "Sets the three Tweaks below at once, the way Winhance's own update policy switch does. Hold updates back makes Windows ask before it downloads anything, keeps the next Windows version a year away and each month's fixes a week away: security updates then arrive late, and only once you say yes. A mix of the rows below reads as Custom until you pick one here.",
            Category: Category.Update,
            Group: "Update policy",
            Targets: targets,
            Options:
            [
                // Every covered Tweak lists the hands-off Option first and the one that holds updates
                // back second, so the preset reads them by position: the three do not share a name
                // for either state.
                Preset("settings-decides", "As set in Settings", 0),
                Preset("hold-back", "Hold updates back", 1),
            ]);

        TweakOption Preset(string id, string label, int index)
        {
            var values = new Dictionary<TweakTarget, MachineValue?>();
            foreach (var tweak in covered)
            {
                var option = tweak.Options[index];
                foreach (var target in tweak.Targets)
                {
                    values[target] = option.Values[target];
                }
            }

            return new TweakOption(id, label, values);
        }
    }

    private static DeclaredTweak AutomaticUpdates()
    {
        // The Configure Automatic Updates policy. AUOptions is only read while NoAutoUpdate is 0 or
        // absent, so the Options that leave updates on delete NoAutoUpdate rather than write 0, which
        // is what Winhance's own mapping does too.
        TweakTarget[] noAutoUpdate = [.. AutoUpdatePolicy("NoAutoUpdate")];
        TweakTarget[] auOptions = [.. AutoUpdatePolicy("AUOptions")];

        return new DeclaredTweak(
            Id: "update.automatic-updates",
            Title: "Automatic updates",
            Description: "How Windows Update fetches and installs updates. Anything but As set in Settings means security fixes wait on you: nothing installs until you answer the notice, or until you open Windows Update yourself. Never check on its own leaves this PC without security updates for as long as it is set. This is a Group Policy, so it also greys out part of the Windows Update page; Windows Home honours it less reliably than Pro and may update anyway.",
            Category: Category.Update,
            Group: "Update policy",
            Targets: [.. noAutoUpdate, .. auOptions],
            Options:
            [
                Mode("settings-decides", "As set in Settings", null, null),
                Mode("notify-before-download", "Notify before downloading", null, 2),
                Mode("download-then-ask", "Download, then ask before installing", null, 3),
                Mode("off", "Never check on its own", 1, 1),
            ]);

        TweakOption Mode(string id, string label, int? noAuto, int? options)
        {
            var values = new Dictionary<TweakTarget, MachineValue?>();
            foreach (var target in noAutoUpdate)
            {
                values[target] = noAuto is { } value ? RegistryValue.DWord(value) : null;
            }

            foreach (var target in auOptions)
            {
                values[target] = options is { } value ? RegistryValue.DWord(value) : null;
            }

            return new TweakOption(id, label, values);
        }
    }

    /// <summary>
    /// A deferral Tweak: Windows' own schedule, or the Windows Update for Business policy values that
    /// hold a class of update back. Winhance writes the same value names under
    /// HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings, where Windows 10's "choose when updates are
    /// installed" page kept them; that page is gone and Windows 11 does not read it back. The policy
    /// key is the documented home for these, so that is what this writes — ADR-0003 allows Microsoft's
    /// own documentation as the source for a value.
    /// </summary>
    private static DeclaredTweak UpdateDeferral(
        string id, string title, string optionId, string optionLabel, string description,
        (string Name, int Value)[] policy)
    {
        TweakTarget[] targets = policy
            .Select(value => new TweakTarget(new RegistryLocation(RegistryHive.LocalMachine, WindowsUpdatePolicies, value.Name)))
            .ToArray();

        return new DeclaredTweak(
            Id: id,
            Title: title,
            Description: description,
            Category: Category.Update,
            Group: "Update policy",
            Targets: targets,
            Options:
            [
                Option(targets, "windows-default", "Windows default", null),
                new TweakOption(optionId, optionLabel, targets
                    .Zip(policy, (target, value) => (Target: target, value.Value))
                    .ToDictionary(pair => pair.Target, pair => (MachineValue?)RegistryValue.DWord(pair.Value))),
            ]);
    }

    private static DeclaredTweak RestartWhileSignedIn()
    {
        TweakTarget[] targets = [.. AutoUpdatePolicy("NoAutoRebootWithLoggedOnUsers")];

        return new DeclaredTweak(
            Id: "update.restart-while-signed-in",
            Title: "Automatic restart while someone is signed in",
            Description: "Whether Windows may restart this PC on its own to finish an update while someone is signed in. Never leaves the restart to you, so an update sits half-installed — and the fixes in it are not yet in force — until you get round to it. Active hours in Settings is the milder version of the same idea.",
            Category: Category.Update,
            Group: "Update policy",
            Targets: targets,
            Options:
            [
                Option(targets, "settings-decides", "As set in Settings", null),
                DWordOption(targets, "never", "Never while someone is signed in", 1),
            ]);
    }

    private static DeclaredTweak ShutdownOption()
    {
        TweakTarget[] targets = [.. AutoUpdatePolicy("NoAUShutdownOption")];

        return new DeclaredTweak(
            Id: "update.shutdown-option",
            Title: "Update and shut down in the power menu",
            Description: "The Update and shut down and Update and restart entries Windows adds to the power menu while an update is waiting. Hidden, plain Shut down and Restart are all you get; the update still installs at a later restart, Windows just stops offering to do it now.",
            Category: Category.Update,
            Group: "Update policy",
            Targets: targets,
            Options:
            [
                Option(targets, "show", "Show them", null),
                DWordOption(targets, "hide", "Hide them", 1),
            ]);
    }

    private static DeclaredTweak UpdateNotifications()
    {
        // SetUpdateNotificationLevel: 1 drops everything but the restart warnings, 2 drops those too.
        // Winhance maps its toggle to 2 for on and 1 for off, which cannot be right for a switch
        // called "show or hide notifications", so the three documented levels are the Options here.
        TweakTarget[] targets =
        [
            new(new RegistryLocation(RegistryHive.LocalMachine, WindowsUpdatePolicies, "SetUpdateNotificationLevel")),
            new(new RegistryLocation(RegistryHive.CurrentUser, WindowsUpdatePolicies, "SetUpdateNotificationLevel")),
        ];

        return new DeclaredTweak(
            Id: "update.update-notifications",
            Title: "Windows Update notifications",
            Description: "How much Windows Update says while it works. Only restart warnings drops the rest of them; None hides the restart warning as well, so Windows can restart this PC to finish an update with nothing said beforehand. This is a Group Policy, so it has the last word over the notification switches in Settings.",
            Category: Category.Update,
            Group: "Update behaviour",
            Targets: targets,
            Options:
            [
                Option(targets, "settings-decides", "As set in Settings", null),
                DWordOption(targets, "restart-warnings-only", "Only restart warnings", 1),
                DWordOption(targets, "none", "None, not even restart warnings", 2),
            ]);
    }

    private static DeclaredTweak DriverUpdates()
    {
        TweakTarget[] targets =
        [
            new(new RegistryLocation(RegistryHive.LocalMachine, WindowsUpdatePolicies, "ExcludeWUDriversInQualityUpdate")),
            new(new RegistryLocation(RegistryHive.CurrentUser, WindowsUpdatePolicies, "ExcludeWUDriversInQualityUpdate")),
        ];

        return new DeclaredTweak(
            Id: "update.drivers",
            Title: "Hardware drivers with Windows Update",
            Description: "Whether a monthly update may bring hardware drivers with it. Left out, Windows stops replacing a driver you picked yourself — and stops supplying one for hardware it has no driver for, so a device you plug in later may fall back to a generic driver, or to none. Drivers you install yourself are untouched either way.",
            Category: Category.Update,
            Group: "Update content",
            Targets: targets,
            Options:
            [
                Option(targets, "include", "Include them", null),
                DWordOption(targets, "exclude", "Leave them out", 1),
            ]);
    }

    private static DeclaredTweak DeliveryOptimization()
    {
        TweakTarget[] targets =
        [
            new(new RegistryLocation(RegistryHive.LocalMachine, DeliveryOptimizationPolicies, "DODownloadMode")),
            new(new RegistryLocation(RegistryHive.CurrentUser, DeliveryOptimizationPolicies, "DODownloadMode")),
        ];
        var off = DWordOption(targets, "off", "Off", 99);

        return new DeclaredTweak(
            Id: "update.delivery-optimization",
            Title: "Delivery Optimization",
            Description: "Whether this PC swaps update and Store downloads with other PCs instead of taking everything from Microsoft. Off means every download comes straight from Microsoft and nothing is uploaded to anyone, which keeps the upload clear while you play; on a network with several Windows PCs on it, downloads can take longer. Nothing here changes which updates arrive.",
            Category: Category.Update,
            Group: "Delivery & Store",
            Targets: targets,
            Options:
            [
                Option(targets, "settings-decides", "As set in Settings", null),
                DWordOption(targets, "this-network", "PCs on this network only", 1),
                DWordOption(targets, "network-and-internet", "PCs on this network and the internet", 3),
                off,
            ],
            Recommended: off);
    }

    private static DeclaredTweak StoreAppUpdates()
    {
        // AutoDownload: 4 updates apps by itself, 2 never does, and a missing value behaves as 4.
        TweakTarget[] targets =
        [
            new(new RegistryLocation(RegistryHive.LocalMachine, StorePolicies, "AutoDownload"), AbsentMeans: RegistryValue.DWord(4)),
            new(new RegistryLocation(RegistryHive.CurrentUser, StorePolicies, "AutoDownload"), AbsentMeans: RegistryValue.DWord(4)),
        ];

        return new DeclaredTweak(
            Id: "update.store-app-updates",
            Title: "Update Microsoft Store apps automatically",
            Description: "Whether the Microsoft Store updates installed apps by itself in the background. Off, every app keeps the version it has — security fixes included — until you open the Store and update it by hand.",
            Category: Category.Update,
            Group: "Delivery & Store",
            Targets: targets,
            Options:
            [
                DWordOption(targets, "on", "On", 4),
                DWordOption(targets, "off", "Off", 2),
            ]);
    }

    /// <summary>
    /// An On/Off Tweak over one of the machine values behind Settings &gt; Windows Update. None of
    /// these exists on a PC where nobody has touched the switch, and Windows reads a missing one as
    /// off, so a fresh Windows reads as Off here rather than as Custom.
    /// </summary>
    private static DeclaredTweak UxSettingOnOff(string id, string title, string description, string name)
    {
        var target = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, UpdateUxSettings, name),
            AbsentMeans: RegistryValue.DWord(0));

        return new DeclaredTweak(id, title, description, Category.Update, "Update behaviour", [target],
            [DWordOption(target, "on", "On", 1), DWordOption(target, "off", "Off", 0)]);
    }

    /// <summary>
    /// One Automatic Updates policy value, for the machine and for the user: the pair Winhance writes
    /// for every value under the AU key. The Windows Update Agent reads the machine copy.
    /// </summary>
    private static IEnumerable<TweakTarget> AutoUpdatePolicy(string name) =>
    [
        new(new RegistryLocation(RegistryHive.LocalMachine, AutoUpdatePolicies, name)),
        new(new RegistryLocation(RegistryHive.CurrentUser, AutoUpdatePolicies, name)),
    ];
}
