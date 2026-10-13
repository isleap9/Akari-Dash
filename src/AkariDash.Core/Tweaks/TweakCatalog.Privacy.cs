using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

// Privacy Tweaks, written from Akari-OS's reg.reg (MIT), Winhance as of its last MIT commit
// (4dc441a0; see THIRD-PARTY-NOTICES.md) and Microsoft's WindowsAI Policy CSP documentation.
// Recommended Options are for performance, so only automatic app installs, which download and
// install apps in the background, has one.
public static partial class TweakCatalog
{
    private const string ConsentStore = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";
    private const string SearchSettings = @"Software\Microsoft\Windows\CurrentVersion\SearchSettings";
    private const string WindowsAIPolicies = @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI";
    private const string DataCollectionPolicies = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection";

    private static IEnumerable<DeclaredTweak> PrivacyTweaks() =>
    [
        DiagnosticData(),
        FeedbackFrequency(),
        ImproveInkingAndTyping(),
        ErrorReporting(),
        SuggestionsPreset(),
        TailoredExperiences(),
        AutomaticAppInstalls(),
        ContentDeliveryAllowed(),
        SubscribedContent(),
        FeatureSuggestions(),
        TipsAfterUpdates(),
        ManufacturersApps(),
        SuggestedApps(),
        SuggestedAppsHistory(),
        SuggestionsInSettings(),
        AdvertisingId(),
        AccountNotifications(),
        OneDriveFolderBackup(),
        WindowsSpotlight(),
        LockScreenTips(),
        LockScreenSlideshow(),
        ActivityHistory(),
        TimelineSuggestions(),
        WebSearchInStart(),
        RecentItems(),
        AppLaunchTracking(),
        SearchHistory(),
        SearchHighlights(),
        CloudContentSearch(),
        Cortana(),
        LanguageListForWebsites(),
        OnlineSpeechRecognition(),
        NarratorOnlineServices(),
        NarratorScripting(),
        InkingAndTypingDictionary(),
        AppAccess(
            "location", "Location",
            "Lets Windows and apps find out where this PC is. Off also stops Find my device from locating it; locked also sets the Group Policy behind it, so nothing can switch location back on until you undo this.",
            policies:
            [
                new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation"),
                new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation"),
            ]),
        AppAccess("camera", "Camera", "Lets Windows and apps use the camera. Off stops video calls and streaming from seeing you.", "webcam"),
        AppAccess("microphone", "Microphone", "Lets Windows and apps use the microphone. Off stops voice chat, calls and streaming from hearing you."),
        AppAccess("account-info", "Account info", "Lets apps read your name, picture and other details of the account you are signed in with.", "userAccountInformation"),
        AppAccess("app-diagnostics", "App diagnostics", "Lets apps read diagnostic details about the other apps running on this PC, such as their name, user name and memory use.", "appDiagnostics"),
        Copilot(),
        RecallSnapshots(),
        ClickToDo(),
        AIInPaint(),
        AIInNotepad(),
    ];

    private static DeclaredTweak DiagnosticData()
    {
        // Akari-OS sets the AllowTelemetry policy to 0 — required data only on Home and Pro, none at
        // all on Enterprise and Education — and Winhance sets the same policy for the user as well.
        // Deleting both hands the choice back to Settings.
        //
        // Winhance also writes the older CurrentVersion\Policies\DataCollection pair (AllowTelemetry
        // and MaxTelemetryAllowed) and DiagTrack's ShowedToastAtLevel. None of those is a target here:
        // the VM found MaxTelemetryAllowed and that AllowTelemetry already holding 1 on a machine
        // Akari-Dash had never written them on, so Windows keeps them itself, the way it keeps
        // ShowedToastAtLevel. Owning a value Windows rewrites would leave this Tweak reading Custom
        // for good after an Undo, because Undo puts exactly those values back.
        TweakTarget[] targets =
        [
            new(new RegistryLocation(RegistryHive.LocalMachine, DataCollectionPolicies, "AllowTelemetry")),
            new(new RegistryLocation(RegistryHive.CurrentUser, DataCollectionPolicies, "AllowTelemetry")),
        ];

        return new DeclaredTweak(
            Id: "privacy.diagnostic-data",
            Title: "Diagnostic data",
            Description: "How much data about your PC and how you use it Windows sends to Microsoft. Minimum sends only what Windows needs to stay secure and up to date (nothing at all on Enterprise and Education), and locks the choice in Settings.",
            Category: Category.Privacy,
            Group: "Diagnostics & feedback",
            Targets: targets,
            Options:
            [
                DWordOption(targets, "minimum", "Minimum", 0),
                Option(targets, "settings-decides", "As set in Settings", null),
            ],
            Activation: Activation.AfterRestart);
    }

    private static DeclaredTweak FeedbackFrequency()
    {
        // Akari-OS sets NumberOfSIUFInPeriod to 0 and deletes PeriodInNanoSeconds ("Never").
        // With neither value, Windows asks automatically. Winhance instead sets the
        // DoNotShowFeedbackNotifications policy for the machine and the user, which is the Group
        // Policy for the same thing, so it is the third Option here rather than part of Never:
        // a PC where only the Siuf values say never has to keep reading as Never.
        var count = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod"));
        var period = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Siuf\Rules", "PeriodInNanoSeconds"));
        TweakTarget[] policies =
        [
            new(new RegistryLocation(RegistryHive.LocalMachine, DataCollectionPolicies, "DoNotShowFeedbackNotifications")),
            new(new RegistryLocation(RegistryHive.CurrentUser, DataCollectionPolicies, "DoNotShowFeedbackNotifications")),
        ];

        return new DeclaredTweak(
            Id: "privacy.feedback-frequency",
            Title: "Feedback requests",
            Description: "How often Windows pops up asking for your feedback. Locked also sets the Group Policy behind it, so nothing can ask again until you undo this.",
            Category: Category.Privacy,
            Group: "Diagnostics & feedback",
            Targets: [count, period, .. policies],
            Options:
            [
                Never("never", "Never", locked: false),
                Never("never-locked", "Never, and locked", locked: true),
                Option([count, period, .. policies], "automatically", "Automatically", null),
            ]);

        TweakOption Never(string id, string label, bool locked)
        {
            var values = new Dictionary<TweakTarget, MachineValue?> { [count] = RegistryValue.DWord(0), [period] = null };
            foreach (var policy in policies)
            {
                values[policy] = locked ? RegistryValue.DWord(1) : null;
            }

            return new TweakOption(id, label, values);
        }
    }

    private static DeclaredTweak TailoredExperiences() =>
        // Akari-OS turns both Settings values off; Winhance also sets the CloudContent policy for the
        // machine and the user, which is the locked Option here. Windows treats missing values as on.
        OnByDefaultOrLocked(
            "privacy.tailored-experiences",
            "Tailored experiences",
            "Lets Microsoft use your diagnostic data to show you personalised tips, ads and recommendations. Locked also sets the Group Policy behind it, so it stays off until you undo this.",
            "Ads & suggestions",
            [
                (@"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled"),
                (@"Software\Microsoft\Windows\CurrentVersion\CPSS\Store\TailoredExperiencesWithDiagnosticDataEnabled", "Value"),
            ],
            [
                new RegistryLocation(RegistryHive.LocalMachine, @"Software\Policies\Microsoft\Windows\CloudContent", "DisableTailoredExperiencesWithDiagnosticData"),
                new RegistryLocation(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\CloudContent", "DisableTailoredExperiencesWithDiagnosticData"),
            ]);

    private static DeclaredTweak ActivityHistory()
    {
        // Akari-OS sets the PublishUserActivities policy to 0, Winhance for the user as well;
        // deleting them hands the choice back to Settings.
        TweakTarget[] targets =
        [
            new(new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities")),
            new(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities")),
        ];

        return new DeclaredTweak(
            Id: "privacy.activity-history",
            Title: "Activity history",
            Description: "Windows keeps a history of the apps, files and websites you use on this PC.",
            Category: Category.Privacy,
            Group: "Activity & search",
            Targets: targets,
            Options:
            [
                DWordOption(targets, "off", "Off", 0),
                Option(targets, "settings-decides", "As set in Settings", null),
            ],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak WebSearchInStart()
    {
        // Akari-OS sets the DisableSearchBoxSuggestions policy to 1. Deleting it only lifts the block:
        // whether Start then searches the web is up to Windows (and is off in some regions).
        var policy = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions"));

        return new DeclaredTweak(
            Id: "privacy.web-search-in-start",
            Title: "Web results in Start search",
            Description: "Searching from Start also sends what you type to Bing and mixes web results in with your apps and files.",
            Category: Category.Privacy,
            Group: "Activity & search",
            Targets: [policy],
            Options: [Option(policy, "blocked", "Blocked", RegistryValue.DWord(1)), Option(policy, "not-blocked", "Not blocked", null)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak RecentItems()
    {
        // Akari-OS turns Start_TrackDocs off. Windows treats a missing value as on.
        var trackDocs = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackDocs"),
            AbsentMeans: RegistryValue.DWord(1));

        return new DeclaredTweak(
            Id: "privacy.recent-items",
            Title: "Recently opened items",
            Description: "Shows the files you opened recently in Start, jump lists and File Explorer.",
            Category: Category.Privacy,
            Group: "Activity & search",
            Targets: [trackDocs],
            Options: [DWordOption(trackDocs, "off", "Off", 0), DWordOption(trackDocs, "on", "On", 1)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak AutomaticAppInstalls()
    {
        // Akari-OS turns SilentInstalledAppsEnabled off. Windows treats a missing value as on.
        var silentInstalls = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled"),
            AbsentMeans: RegistryValue.DWord(1));
        var off = DWordOption(silentInstalls, "off", "Off", 0);

        return new DeclaredTweak(
            Id: "privacy.automatic-app-installs",
            Title: "Automatic app installs",
            Description: "Windows downloads and installs suggested apps from the Store in the background without asking. Apps already installed stay.",
            Category: Category.Privacy,
            Group: "Ads & suggestions",
            Targets: [silentInstalls],
            Options: [off, DWordOption(silentInstalls, "on", "On", 1)],
            Recommended: off);
    }

    private static DeclaredTweak SuggestionsInSettings()
    {
        // Akari-OS turns these SubscribedContent values off; they are the ones behind "Show me
        // suggested content in the Settings app". Windows treats missing values as on.
        TweakTarget[] targets =
        [
            SuggestionTarget("SubscribedContent-338393Enabled"),
            SuggestionTarget("SubscribedContent-353694Enabled"),
            SuggestionTarget("SubscribedContent-353696Enabled"),
        ];

        return new DeclaredTweak(
            Id: "privacy.suggestions-in-settings",
            Title: "Suggested content in Settings",
            Description: "Shows suggestions and promotions for Microsoft apps and services inside the Settings app.",
            Category: Category.Privacy,
            Group: "Ads & suggestions",
            Targets: targets,
            Options: [DWordOption(targets, "off", "Off", 0), DWordOption(targets, "on", "On", 1)]);

        static TweakTarget SuggestionTarget(string name) => new(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", name),
            AbsentMeans: RegistryValue.DWord(1));
    }

    /// <summary>
    /// An Off/On Tweak over one or more current-user DWORDs. Windows treats a missing value as
    /// <paramref name="absentMeans"/>, which is on (1) for everything here except the lock screen
    /// slideshow, which a fresh install leaves off.
    /// </summary>
    private static DeclaredTweak OnByDefault(
        string id, string title, string description, string group, IEnumerable<(string Key, string Name)> values,
        Activation activation = Activation.Immediately, int absentMeans = 1)
    {
        TweakTarget[] targets = values
            .Select(value => new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, value.Key, value.Name), AbsentMeans: RegistryValue.DWord(absentMeans)))
            .ToArray();

        return new DeclaredTweak(
            Id: id,
            Title: title,
            Description: description,
            Category: Category.Privacy,
            Group: group,
            Targets: targets,
            Options: [DWordOption(targets, "off", "Off", 0), DWordOption(targets, "on", "On", 1)],
            Activation: activation);
    }

    /// <summary>
    /// An Off/On Tweak over current-user Settings values that also offers to lock the choice in with
    /// the Group Policy values Winhance writes for it. "Off" writes only what the switch in Settings
    /// writes, so a PC where someone used Settings still reads as Off; "Off, and locked" writes the
    /// policies too, which stops Windows and other apps turning it back on and greys the switch out;
    /// "On" deletes the policies again. <paramref name="policyOff"/> is what the policy holds to turn
    /// the setting off, which is 0 for the policies named "Allow...".
    /// </summary>
    private static DeclaredTweak OnByDefaultOrLocked(
        string id, string title, string description, string group,
        (string Key, string Name)[] values, RegistryLocation[] policies,
        Activation activation = Activation.Immediately, int absentMeans = 1, int policyOff = 1)
    {
        TweakTarget[] settings = values
            .Select(value => new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, value.Key, value.Name), AbsentMeans: RegistryValue.DWord(absentMeans)))
            .ToArray();
        TweakTarget[] locks = policies.Select(policy => new TweakTarget(policy)).ToArray();

        return new DeclaredTweak(
            Id: id,
            Title: title,
            Description: description,
            Category: Category.Privacy,
            Group: group,
            Targets: [.. settings, .. locks],
            Options:
            [
                Values("off", "Off", 0, locked: false),
                Values("off-locked", "Off, and locked", 0, locked: true),
                Values("on", "On", 1, locked: false),
            ],
            Activation: activation);

        TweakOption Values(string optionId, string label, int setting, bool locked)
        {
            var written = settings.ToDictionary(target => target, target => (MachineValue?)RegistryValue.DWord(setting));
            foreach (var padlock in locks)
            {
                written[padlock] = locked ? RegistryValue.DWord(policyOff) : null;
            }

            return new TweakOption(optionId, label, written);
        }
    }

    /// <summary>
    /// A policy Tweak: "off" sets every policy value to <paramref name="offValue"/>; deleting them
    /// hands the choice back. Policies that turn something off hold 1, except the ones named
    /// "Allow..." (AllowCortana), which turn it off by holding 0.
    /// </summary>
    private static DeclaredTweak OffByPolicy(
        string id, string title, string description, string group, IEnumerable<RegistryLocation> policies,
        Activation activation = Activation.Immediately, int offValue = 1, string offLabel = "Off",
        string handBackLabel = "Not blocked")
    {
        TweakTarget[] targets = policies.Select(policy => new TweakTarget(policy)).ToArray();

        return new DeclaredTweak(
            Id: id,
            Title: title,
            Description: description,
            Category: Category.Privacy,
            Group: group,
            Targets: targets,
            Options: [DWordOption(targets, "off", offLabel, offValue), Option(targets, "not-blocked", handBackLabel, null)],
            Activation: activation);
    }

    private static DeclaredTweak ImproveInkingAndTyping() =>
        // Winhance (MIT) turns TIPC off. It also writes a CPSS\Store value, but Windows leaves that
        // missing when this is turned off during setup (seen in the VM), so only TIPC tells the state.
        OnByDefault(
            "privacy.improve-inking-typing",
            "Improve inking and typing",
            "Sends Microsoft samples of what you type and write by hand, to improve recognition and suggestions.",
            "Diagnostics & feedback",
            [(@"Software\Microsoft\Input\TIPC", "Enabled")]);

    private static DeclaredTweak ErrorReporting() =>
        // Winhance sets the Windows Error Reporting policy for the machine and the user. It is the
        // policy behind the service on the Services page: with the policy set, nothing is reported
        // even while the service runs. Deleting it hands the choice back to Windows.
        OffByPolicy(
            "privacy.error-reporting",
            "Error reporting",
            "Sends Microsoft a report when an app or Windows crashes, with what the program was doing at the time. Off keeps crashes to this PC, and Reliability Monitor shows less about them.",
            "Diagnostics & feedback",
            [
                new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled"),
                new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled"),
            ],
            handBackLabel: "As set in Windows");

    private static DeclaredTweak AdvertisingId() =>
        // The Settings toggle, plus the AdvertisingInfo policies Winhance sets for the machine and
        // the user ("Turn off the advertising ID"), offered as the locked Option.
        OnByDefaultOrLocked(
            "privacy.advertising-id",
            "Advertising ID",
            "Gives apps an ID that follows you from app to app, so they can show you ads based on what you do. Locked also sets the Group Policy behind it, so apps cannot ask for the ID again until you undo this.",
            "Ads & suggestions",
            [(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled")],
            [
                new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy"),
                new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy"),
            ]);

    private static DeclaredTweak LockScreenTips() =>
        // Akari-OS and Winhance turn RotatingLockScreenOverlayEnabled off.
        OnByDefault(
            "privacy.lock-screen-tips",
            "Fun facts and tips on the lock screen",
            "Puts fun facts, tips and promotions from Microsoft on top of the lock screen picture.",
            "Lock screen",
            [(ContentDelivery, "RotatingLockScreenOverlayEnabled")]);

    // The rest of the ContentDeliveryManager family, each value its own Tweak as Winhance (MIT) has
    // them. Windows treats every one of them as on when it is missing, and reads them at sign-in.
    /// <summary>
    /// The Tweaks the suggestions preset sets at once: every value Windows keeps under
    /// ContentDeliveryManager, which is what Winhance's own Ads, Suggestions and Promotional Content
    /// switch covers. Built by calling each Tweak, which is a pure function, so the preset reads the
    /// values from the Tweaks themselves rather than repeating them.
    /// </summary>
    private static DeclaredTweak[] SuggestionTweaks() =>
    [
        AutomaticAppInstalls(),
        ContentDeliveryAllowed(),
        SubscribedContent(),
        FeatureSuggestions(),
        TipsAfterUpdates(),
        ManufacturersApps(),
        SuggestedApps(),
        SuggestedAppsHistory(),
        SuggestionsInSettings(),
        WindowsSpotlight(),
        LockScreenTips(),
        LockScreenSlideshow(),
        TimelineSuggestions(),
    ];

    private static DeclaredTweak SuggestionsPreset()
    {
        var suggestions = SuggestionTweaks();
        TweakTarget[] targets = [.. suggestions.SelectMany(tweak => tweak.Targets)];

        return new DeclaredTweak(
            Id: "privacy.suggestions-preset",
            Title: "Suggestions and promotions",
            Description: "Sets every suggestion Tweak at once, the ones below and the three on the lock screen, the way Winhance's own switch does. Off stops Windows fetching suggested apps, promotions, spotlight pictures and tips from Microsoft. A mix of settings reads as Custom until you pick one here.",
            Category: Category.Privacy,
            Group: "Ads & suggestions",
            Targets: targets,
            Options: [Preset("off", "All off", "off"), Preset("on", "All on (Windows defaults)", "on")],
            Activation: Activation.AfterSignOut);

        TweakOption Preset(string id, string label, string childOption)
        {
            var values = new Dictionary<TweakTarget, MachineValue?>();
            foreach (var tweak in suggestions)
            {
                var option = tweak.Options.Single(candidate => candidate.Id == childOption);
                foreach (var target in tweak.Targets)
                {
                    values[target] = option.Values[target];
                }
            }

            return new TweakOption(id, label, values);
        }
    }

    private static DeclaredTweak ContentDeliveryAllowed() =>
        OnByDefault(
            "privacy.content-delivery",
            "Content delivery",
            "The one switch Windows checks before fetching any suggestion, promotion or lock screen picture from Microsoft. Off stops the lot at once, whatever the Tweaks below it are set to.",
            "Ads & suggestions",
            [(ContentDelivery, "ContentDeliveryAllowed")],
            Activation.AfterSignOut);

    private static DeclaredTweak SubscribedContent() =>
        OnByDefault(
            "privacy.subscribed-content",
            "Subscribed content",
            "Lets Windows subscribe to the feed of promotions from Microsoft and its partners that most of the suggestions in Start, Settings and the lock screen are drawn from.",
            "Ads & suggestions",
            [(ContentDelivery, "SubscribedContentEnabled")],
            Activation.AfterSignOut);

    private static DeclaredTweak FeatureSuggestions() =>
        OnByDefault(
            "privacy.feature-suggestions",
            "Feature suggestions",
            "Lets Microsoft switch new Windows features on for this PC after setup, and advertise them to you.",
            "Ads & suggestions",
            [(ContentDelivery, "FeatureManagementEnabled")],
            Activation.AfterSignOut);

    private static DeclaredTweak TipsAfterUpdates() =>
        OnByDefault(
            "privacy.tips-after-updates",
            "Tips and welcome messages",
            "Shows Microsoft's tips, tricks and promotions as notifications, the ones that arrive after Windows has updated itself.",
            "Ads & suggestions",
            [(ContentDelivery, "SoftLandingEnabled")],
            Activation.AfterSignOut);

    private static DeclaredTweak ManufacturersApps() =>
        OnByDefault(
            "privacy.manufacturers-apps",
            "The manufacturer's bundled apps",
            "Lets Windows put the PC manufacturer's bundled apps back when it resets or refreshes this PC. Apps already installed stay where they are.",
            "Ads & suggestions",
            [(ContentDelivery, "OemPreInstalledAppsEnabled")],
            Activation.AfterSignOut);

    private static DeclaredTweak SuggestedApps() =>
        OnByDefault(
            "privacy.suggested-apps",
            "Suggested apps in Start",
            "Lets Windows add Microsoft's suggested apps to Start, and put them back when it resets this PC. Apps already installed stay where they are.",
            "Ads & suggestions",
            [(ContentDelivery, "PreInstalledAppsEnabled")],
            Activation.AfterSignOut);

    private static DeclaredTweak SuggestedAppsHistory() =>
        OnByDefault(
            "privacy.suggested-apps-history",
            "Suggested apps history",
            "Windows records whether it has ever put suggested apps on this PC, and uses that to decide what to install again later.",
            "Ads & suggestions",
            [(ContentDelivery, "PreInstalledAppsEverEnabled")],
            Activation.AfterSignOut);

    private static DeclaredTweak OneDriveFolderBackup() =>
        // Winhance sets KFMBlockOptIn for the user and the machine; Microsoft documents it as
        // "Prevent users from moving their Windows known folders to OneDrive".
        OffByPolicy(
            "privacy.onedrive-folder-backup",
            "OneDrive folder backup prompts",
            "Blocks OneDrive from asking to back up your Desktop, Documents and Pictures, and from moving them into OneDrive on its own. Folders already backed up stay as they are.",
            "Ads & suggestions",
            [
                new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\OneDrive", "KFMBlockOptIn"),
                new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Policies\Microsoft\OneDrive", "KFMBlockOptIn"),
            ],
            offLabel: "Blocked");

    private static DeclaredTweak WindowsSpotlight() =>
        // Winhance turns RotatingLockScreenEnabled off. Windows treats a missing value as on.
        OnByDefault(
            "privacy.windows-spotlight",
            "Windows spotlight on the lock screen",
            "Puts Microsoft's rotating lock screen pictures, and the tips and promotions that come with them, on the lock screen instead of your own picture.",
            "Lock screen",
            [(ContentDelivery, "RotatingLockScreenEnabled")],
            Activation.AfterSignOut);

    private static DeclaredTweak LockScreenSlideshow() =>
        // Winhance turns SlideshowEnabled off. Unlike the rest of this family, a fresh install leaves
        // it missing and off: the lock screen shows one picture rather than a slideshow.
        OnByDefault(
            "privacy.lock-screen-slideshow",
            "Lock screen slideshow",
            "Plays your own pictures as a slideshow on the lock screen, which means Windows reads those folders while the PC is locked.",
            "Lock screen",
            [(ContentDelivery, "SlideshowEnabled")],
            Activation.AfterSignOut,
            absentMeans: 0);

    private static DeclaredTweak TimelineSuggestions() =>
        // Winhance turns SubscribedContent-353698Enabled off: the suggestions in Task view.
        OnByDefault(
            "privacy.timeline-suggestions",
            "Suggestions in Task view",
            "Puts Microsoft's suggestions in Task view, next to the windows you actually have open.",
            "Activity & search",
            [(ContentDelivery, "SubscribedContent-353698Enabled")],
            Activation.AfterSignOut);

    private static DeclaredTweak AccountNotifications() =>
        // Akari-OS and Winhance turn EnableAccountNotifications off.
        OnByDefault(
            "privacy.account-notifications",
            "Account notifications in Settings",
            "Shows prompts in Settings to sign in with a Microsoft account, back up this PC or manage subscriptions.",
            "Ads & suggestions",
            [(@"Software\Microsoft\Windows\CurrentVersion\SystemSettings\AccountNotifications", "EnableAccountNotifications")]);

    private static DeclaredTweak AppLaunchTracking() =>
        // Winhance turns Start_TrackProgs off (the Settings toggle); Akari-OS uses the EdgeUI
        // DisableMFUTracking policy for the same thing.
        OnByDefault(
            "privacy.app-launch-tracking",
            "App launch tracking",
            "Windows keeps track of which apps you open, to rank them in Start and search.",
            "Activity & search",
            [(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs")],
            Activation.AfterSignOut);

    private static DeclaredTweak SearchHistory() =>
        // Winhance turns IsDeviceSearchHistoryEnabled off.
        OnByDefault(
            "privacy.search-history",
            "Search history on this PC",
            "Windows remembers what you searched for on this PC to suggest it again. Off also clears it.",
            "Activity & search",
            [(SearchSettings, "IsDeviceSearchHistoryEnabled")]);

    private static DeclaredTweak SearchHighlights() =>
        // Akari-OS and Winhance turn IsDynamicSearchBoxEnabled off.
        OnByDefault(
            "privacy.search-highlights",
            "Search highlights",
            "Fills the search box and search home with trending searches, holidays and other content from Bing.",
            "Activity & search",
            [(SearchSettings, "IsDynamicSearchBoxEnabled")]);

    private static DeclaredTweak CloudContentSearch() =>
        // Akari-OS and Winhance turn both off; one is for Microsoft accounts, one for work or school.
        OnByDefault(
            "privacy.cloud-content-search",
            "Cloud content in search",
            "Searching from Start also looks through your OneDrive, Outlook and other Microsoft account or work or school content.",
            "Activity & search",
            [(SearchSettings, "IsMSACloudSearchEnabled"), (SearchSettings, "IsAADCloudSearchEnabled")]);

    private static DeclaredTweak Cortana() =>
        // Winhance sets AllowCortana for the machine and the user. Microsoft documents it the other
        // way round from most policies: 0 is what blocks Cortana, and a missing value allows it.
        OffByPolicy(
            "privacy.cortana",
            "Cortana",
            "Blocks Cortana from running on this PC. Searching from Start is not affected: only the assistant itself is blocked.",
            "Activity & search",
            [
                new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana"),
                new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana"),
            ],
            Activation.AfterSignOut,
            offValue: 0,
            offLabel: "Blocked");

    private static DeclaredTweak NarratorOnlineServices() =>
        // Winhance turns Narrator's OnlineServicesEnabled off. Windows treats a missing value as on.
        OnByDefault(
            "privacy.narrator-online-services",
            "Narrator online services",
            "Lets Narrator use Microsoft's online voices and image descriptions, which sends what it reads out to Microsoft. Off keeps the voices that run on this PC.",
            "Speech, inking & typing",
            [(@"Software\Microsoft\Narrator\NoRoam", "OnlineServicesEnabled")]);

    private static DeclaredTweak NarratorScripting() =>
        // Winhance turns Narrator's ScriptingEnabled off. Windows treats a missing value as on.
        OnByDefault(
            "privacy.narrator-scripting",
            "Narrator scripting",
            "Lets apps run scripts that drive Narrator and read what it has on screen. Off stops those scripts; Narrator itself still works.",
            "Speech, inking & typing",
            [(@"Software\Microsoft\Narrator\NoRoam", "ScriptingEnabled")]);

    private static DeclaredTweak LanguageListForWebsites()
    {
        // Akari-OS and Winhance opt out with HttpAcceptLanguageOptOut = 1. Windows treats a missing
        // value as sharing the list.
        var optOut = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Control Panel\International\User Profile", "HttpAcceptLanguageOptOut"),
            AbsentMeans: RegistryValue.DWord(0));

        return new DeclaredTweak(
            Id: "privacy.language-list",
            Title: "Language list for websites",
            Description: "Tells websites which languages you use, so they can show pages in your language without asking.",
            Category: Category.Privacy,
            Group: "Activity & search",
            Targets: [optOut],
            Options: [DWordOption(optOut, "off", "Off", 1), DWordOption(optOut, "on", "On", 0)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak OnlineSpeechRecognition() =>
        // Winhance turns HasAccepted off (it is missing until you are asked, and missing means not
        // accepted) and sets the AllowInputPersonalization policy for the machine and the user. That
        // policy turns the setting off by holding 0, not 1, and is the locked Option here.
        OnByDefaultOrLocked(
            "privacy.online-speech",
            "Online speech recognition",
            "Sends your voice to Microsoft's cloud to turn it into text, for dictation and voice typing in apps. Off keeps the on-device recognition; locked also sets the Group Policy behind it, so nothing can accept it again until you undo this.",
            "Speech, inking & typing",
            [(@"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted")],
            [
                new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\InputPersonalization", "AllowInputPersonalization"),
                new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Policies\Microsoft\InputPersonalization", "AllowInputPersonalization"),
            ],
            absentMeans: 0,
            policyOff: 0);

    private static DeclaredTweak InkingAndTypingDictionary()
    {
        // Akari-OS and Winhance (MIT) switch these together, the way the Settings toggle does. Missing
        // values behave as on. Winhance's CPSS\Store value is left out: Windows does not write it.
        TweakTarget[] learning =
        [
            new(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy"), AbsentMeans: RegistryValue.DWord(1)),
            new(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts"), AbsentMeans: RegistryValue.DWord(1)),
        ];
        TweakTarget[] restrictions =
        [
            new(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection"), AbsentMeans: RegistryValue.DWord(0)),
            new(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection"), AbsentMeans: RegistryValue.DWord(0)),
        ];

        return new DeclaredTweak(
            Id: "privacy.inking-typing-dictionary",
            Title: "Personal inking and typing dictionary",
            Description: "Learns words from what you type and write by hand, and from your contacts, to improve suggestions. Off also clears the words it has learned.",
            Category: Category.Privacy,
            Group: "Speech, inking & typing",
            Targets: [.. learning, .. restrictions],
            Options:
            [
                Values("off", "Off", 0, 1),
                Values("on", "On", 1, 0),
            ]);

        TweakOption Values(string id, string label, int learningValue, int restrictionValue) => new(
            id,
            label,
            learning.Select(target => (target, value: RegistryValue.DWord(learningValue)))
                .Concat(restrictions.Select(target => (target, value: RegistryValue.DWord(restrictionValue))))
                .ToDictionary(pair => pair.target, pair => (MachineValue?)pair.value));
    }

    private static DeclaredTweak AppAccess(
        string name, string title, string description, string? consentKey = null, RegistryLocation[]? policies = null)
    {
        // Akari-OS and Winhance set the machine-wide ConsentStore value: "Allow" or "Deny". A matter
        // of taste, so no Recommended Option: Akari-OS itself turns camera and microphone on.
        // Where Winhance also has a Group Policy for the same permission (location), it is the third,
        // locked Option, so a PC where the permission was simply denied still reads as Off.
        var access = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, $@"{ConsentStore}\{consentKey ?? name}", "Value"),
            AbsentMeans: RegistryValue.String("Allow"));
        TweakTarget[] locks = (policies ?? []).Select(policy => new TweakTarget(policy)).ToArray();

        List<TweakOption> options = [Consent("off", "Off", "Deny", locked: false)];
        if (locks.Length > 0)
        {
            options.Add(Consent("off-locked", "Off, and locked", "Deny", locked: true));
        }

        options.Add(Consent("on", "On", "Allow", locked: false));

        return new DeclaredTweak(
            Id: $"privacy.{name}",
            Title: title,
            Description: description,
            Category: Category.Privacy,
            Group: "App permissions",
            Targets: [access, .. locks],
            Options: options);

        TweakOption Consent(string optionId, string label, string consent, bool locked)
        {
            var values = new Dictionary<TweakTarget, MachineValue?> { [access] = RegistryValue.String(consent) };
            foreach (var padlock in locks)
            {
                values[padlock] = locked ? RegistryValue.DWord(1) : null;
            }

            return new TweakOption(optionId, label, values);
        }
    }

    private static DeclaredTweak Copilot() =>
        // Akari-OS sets TurnOffWindowsCopilot for the user and the machine. Microsoft documents it
        // as deprecated: it turns off the older Copilot pane, not the newer Copilot app.
        OffByPolicy(
            "privacy.copilot",
            "Copilot in Windows",
            "Turns off the Copilot pane built into Windows, and its taskbar button, where Windows has one. The newer Copilot app is not affected: uninstall it from Apps if you do not want it.",
            "Copilot & AI",
            [
                new RegistryLocation(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot"),
                new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot"),
            ],
            activation: Activation.AfterSignOut);

    private static DeclaredTweak RecallSnapshots() =>
        // Akari-OS also sets AllowRecallEnablement to 0, but Microsoft documents that it removes
        // Recall from the PC, which Undo cannot put back; this policy only stops the snapshots.
        OffByPolicy(
            "privacy.recall-snapshots",
            "Recall snapshots",
            "Blocks Recall from saving snapshots of your screen, on Copilot+ PCs that have Recall. Off also deletes the snapshots already saved, and Undo cannot bring them back.",
            "Copilot & AI",
            [new RegistryLocation(RegistryHive.LocalMachine, WindowsAIPolicies, "DisableAIDataAnalysis")],
            activation: Activation.AfterRestart);

    private static DeclaredTweak ClickToDo() =>
        // Akari-OS sets DisableClickToDo; Microsoft documents it in the WindowsAI Policy CSP.
        OffByPolicy(
            "privacy.click-to-do",
            "Click to Do",
            "Removes Click to Do, which takes a screenshot and suggests actions for what is on your screen, on PCs that have it.",
            "Copilot & AI",
            [new RegistryLocation(RegistryHive.LocalMachine, WindowsAIPolicies, "DisableClickToDo")],
            activation: Activation.AfterRestart);

    private static DeclaredTweak AIInPaint() =>
        // Akari-OS sets all three Paint policies; Microsoft documents them in the WindowsAI Policy CSP.
        OffByPolicy(
            "privacy.ai-in-paint",
            "AI features in Paint",
            "Removes Cocreator, Image Creator and generative fill from Paint.",
            "Copilot & AI",
            new[] { "DisableCocreator", "DisableImageCreator", "DisableGenerativeFill" }
                .Select(name => new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", name)));

    private static DeclaredTweak AIInNotepad() =>
        // Akari-OS sets the DisableAIFeatures policy.
        OffByPolicy(
            "privacy.ai-in-notepad",
            "AI features in Notepad",
            "Removes Copilot features such as rewrite and summarize from Notepad.",
            "Copilot & AI",
            [new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Policies\WindowsNotepad", "DisableAIFeatures")]);
}
