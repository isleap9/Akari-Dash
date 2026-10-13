using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

// Notification Tweaks: what Windows and apps may interrupt you with, the ground Settings > System >
// Notifications covers. Written from Winhance as of its last MIT commit (4dc441a0; see
// THIRD-PARTY-NOTICES.md). All are a matter of taste, so none has a Recommended Option.
//
// Tips and suggestions, the welcome experience after updates and Finish setting up this PC moved
// here from the Windows page's Notifications Group, under notifications. ids: they are notifications,
// and this is the page someone looks on for them. A PC that already applied one of them shows it on
// Home as a change from a Tweak no longer in Akari-Dash, with its Undo beside it.
//
// Left out: Winhance greys a row out while the switch above it is off, and some of its rows wait on
// a setting on another page. Every Tweak here reads and writes only its own values instead, and a
// description says plainly when a larger switch above it has the last word.
//
// Known, and harmless: a sender Windows has not used yet has no key of its own, so switching that
// row Off creates one, and Undo deletes the value but leaves the empty key behind. The VM check for
// #30 saw exactly that for Windows.SystemToast.CapabilityAccess. A sender key with no Enabled value
// is a sender that notifies, which is where the machine started, so behaviour is fully restored --
// unlike the classic right-click menu on the Windows page, where the leftover key *is* the setting.
public static partial class TweakCatalog
{
    private const string PushNotifications = @"Software\Microsoft\Windows\CurrentVersion\PushNotifications";
    private const string NotificationSettings = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";
    private const string SecurityCenterPolicies = @"SOFTWARE\Policies\Microsoft\Windows Defender Security Center\Notifications";

    private static IEnumerable<DeclaredTweak> NotificationTweaks() =>
    [
        ShowNotifications(),
        NotificationOnOff("notifications.sounds", "Notification sounds",
            "The chime a notification plays as it arrives. Off leaves the banner silent, and does not touch alarms, reminders or an app's own sounds.",
            "Notifications", NotificationSettings, "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND"),
        LockScreenNotifications(),
        NotificationOnOff("notifications.lock-screen-reminders", "Reminders and calls on the lock screen",
            "Reminders and incoming internet calls on the lock screen. Windows treats these as urgent and shows them even where it hides the rest.",
            "Notifications", NotificationSettings, "NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK"),
        NotificationOnOff("notifications.bell-icon", "Bell icon on the taskbar",
            "The bell beside the clock, which fills in when something is waiting. Off hides it; Win+N still opens the notification centre.",
            "Notifications", ExplorerAdvanced, "ShowNotificationIcon", Activation.AfterSignOut),

        NotificationOnOff("notifications.tips", "Tips and suggestions",
            "Notifications with tips and suggestions as you use Windows.",
            "Suggestions", ContentDelivery, "SubscribedContent-338389Enabled", Activation.AfterSignOut),
        NotificationOnOff("notifications.welcome-experience", "Welcome experience after updates",
            "A full-screen page about what is new after a Windows update, and sometimes after signing in.",
            "Suggestions", ContentDelivery, "SubscribedContent-310093Enabled", Activation.AfterSignOut),
        NotificationOnOff("notifications.finish-setup", "Finish setting up this PC",
            "Reminders to finish setting up Windows, such as signing in to OneDrive or Microsoft 365.",
            "Suggestions", @"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", Activation.AfterSignOut),
        NotificationOnOff("notifications.notification-centre-suggestions", "Suggestions in the notification centre",
            "Suggestions Windows adds to the notification centre itself, under whatever your apps have put there.",
            "Suggestions", ContentDelivery, "SystemPaneSuggestionsEnabled", Activation.AfterSignOut),

        SenderToast("notifications.app-permissions", "App permission requests",
            "The notice Windows shows when an app starts using the camera, the microphone or your location.",
            "CapabilityAccess"),
        SenderToast("notifications.startup-apps", "New startup apps",
            "The notice Windows shows when an app you installed added itself to the apps that start with Windows.",
            "StartupApp"),
        SenderToast("notifications.security-and-maintenance", "Security and Maintenance",
            "Messages from Security and Maintenance: a backup that has not run, a drive reporting trouble, maintenance it could not finish.",
            "SecurityAndMaintenance"),
        NotificationOnOff("notifications.clock-change", "Clock changes",
            "The notice Windows shows when it moves the clock for daylight saving time. The clock still changes either way.",
            "System notifications", @"Control Panel\Desktop", "DstNotification"),
        NotificationOnOff("notifications.location-requests", "Location requests from apps",
            "The prompt Windows shows the first time an app asks for your location. Off lets an app that already has permission use it without a word.",
            "System notifications", @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "ShowGlobalPrompts"),

        WindowsSecurityNotifications(),
    ];

    /// <summary>An On/Off Tweak over one current-user DWORD where 1 is on, which is also what a missing value means.</summary>
    private static DeclaredTweak NotificationOnOff(
        string id, string title, string description, string group, string key, string name,
        Activation activation = Activation.Immediately)
    {
        var target = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, key, name), AbsentMeans: RegistryValue.DWord(1));

        return new DeclaredTweak(id, title, description, Category.Notifications, group, [target],
            [DWordOption(target, "on", "On", 1), DWordOption(target, "off", "Off", 0)],
            Activation: activation);
    }

    /// <summary>An On/Off Tweak over one of Windows' own notification senders, which Windows reads as it raises the notification.</summary>
    private static DeclaredTweak SenderToast(string id, string title, string description, string sender) =>
        NotificationOnOff(id, title, description, "System notifications", $@"{NotificationSettings}\Windows.SystemToast.{sender}", "Enabled");

    private static DeclaredTweak ShowNotifications()
    {
        // The switch above every other row here, including the ones on other pages. Windows restarts
        // WpnUserService when Settings writes it, which Akari-Dash does not, so it waits for a sign-out.
        var toasts = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, PushNotifications, "ToastEnabled"), AbsentMeans: RegistryValue.DWord(1));

        return new DeclaredTweak("notifications.notifications", "Notifications",
            "Banners from apps and from Windows itself, and the notification centre they collect in. Off stops all of them, this app's included, and leaves the rest of this page with nothing to switch. Alarms and timers still sound.",
            Category.Notifications, "Notifications", [toasts],
            [DWordOption(toasts, "on", "On", 1), DWordOption(toasts, "off", "Off", 0)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak LockScreenNotifications()
    {
        // Two values: the notification platform's own, and the one Settings' lock-screen page writes.
        TweakTarget[] targets =
        [
            new(new RegistryLocation(RegistryHive.CurrentUser, NotificationSettings, "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK"), AbsentMeans: RegistryValue.DWord(1)),
            new(new RegistryLocation(RegistryHive.CurrentUser, PushNotifications, "LockScreenToastEnabled"), AbsentMeans: RegistryValue.DWord(1)),
        ];

        return new DeclaredTweak("notifications.lock-screen", "Notifications on the lock screen",
            "Whether notifications show while the PC is locked, where anyone walking past can read them.",
            Category.Notifications, "Notifications", targets,
            [DWordOption(targets, "on", "On", 1), DWordOption(targets, "off", "Off", 0)]);
    }

    private static DeclaredTweak WindowsSecurityNotifications()
    {
        // DisableNotifications, so 1 is off; a missing value means 0, which is on. The machine's own
        // value plus the policy copies for the machine and the user, which is what makes it stick.
        TweakTarget[] targets =
        [
            Silence(RegistryHive.LocalMachine, $@"{SecurityCenterKey}\Notifications", "DisableNotifications"),
            Silence(RegistryHive.LocalMachine, SecurityCenterPolicies, "DisableNotifications"),
            Silence(RegistryHive.CurrentUser, SecurityCenterPolicies, "DisableNotifications"),
            Silence(RegistryHive.LocalMachine, SecurityCenterPolicies, "DisableEnhancedNotifications"),
            Silence(RegistryHive.CurrentUser, SecurityCenterPolicies, "DisableEnhancedNotifications"),
        ];

        return new DeclaredTweak("notifications.windows-security", "Every Windows Security notification",
            "Everything Windows Security says: threats found, scans finished, protection that needs attention. Off silences the lot, so a real threat passes without a word. The Security page switches off the routine ones on their own and leaves the alerts.",
            Category.Notifications, "Windows Security", targets,
            [DWordOption(targets, "on", "On", 0), DWordOption(targets, "off", "Off", 1)],
            Activation: Activation.AfterSignOut);

        static TweakTarget Silence(RegistryHive hive, string key, string name) =>
            new(new RegistryLocation(hive, key, name), AbsentMeans: RegistryValue.DWord(0));
    }
}
