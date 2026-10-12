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

    private static IEnumerable<DeclaredTweak> PrivacyTweaks() =>
    [
        DiagnosticData(),
        FeedbackFrequency(),
        ImproveInkingAndTyping(),
        TailoredExperiences(),
        AutomaticAppInstalls(),
        SuggestionsInSettings(),
        AdvertisingId(),
        LockScreenTips(),
        AccountNotifications(),
        ActivityHistory(),
        WebSearchInStart(),
        RecentItems(),
        AppLaunchTracking(),
        SearchHistory(),
        SearchHighlights(),
        CloudContentSearch(),
        LanguageListForWebsites(),
        OnlineSpeechRecognition(),
        InkingAndTypingDictionary(),
        AppAccess("location", "Location", "Lets Windows and apps find out where this PC is. Off also stops Find my device from locating it."),
        AppAccess("camera", "Camera", "Lets Windows and apps use the camera. Off stops video calls and streaming from seeing you.", "webcam"),
        AppAccess("microphone", "Microphone", "Lets Windows and apps use the microphone. Off stops voice chat, calls and streaming from hearing you."),
        Copilot(),
        RecallSnapshots(),
        ClickToDo(),
        AIInPaint(),
        AIInNotepad(),
    ];

    private static DeclaredTweak DiagnosticData()
    {
        // Akari-OS sets the AllowTelemetry policy to 0: required data only on Home and Pro, none at
        // all on Enterprise and Education. Deleting the policy hands the choice back to Settings.
        var policy = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry"));

        return new DeclaredTweak(
            Id: "privacy.diagnostic-data",
            Title: "Diagnostic data",
            Description: "How much data about your PC and how you use it Windows sends to Microsoft. Minimum sends only what Windows needs to stay secure and up to date (nothing at all on Enterprise and Education), and locks the choice in Settings.",
            Category: Category.Privacy,
            Group: "Diagnostics & feedback",
            Targets: [policy],
            Options:
            [
                Option(policy, "minimum", "Minimum", RegistryValue.DWord(0)),
                Option(policy, "settings-decides", "As set in Settings", null),
            ],
            Activation: Activation.AfterRestart);
    }

    private static DeclaredTweak FeedbackFrequency()
    {
        // Akari-OS sets NumberOfSIUFInPeriod to 0 and deletes PeriodInNanoSeconds ("Never").
        // With neither value, Windows asks automatically.
        var count = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod"));
        var period = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Siuf\Rules", "PeriodInNanoSeconds"));

        return new DeclaredTweak(
            Id: "privacy.feedback-frequency",
            Title: "Feedback requests",
            Description: "How often Windows pops up asking for your feedback.",
            Category: Category.Privacy,
            Group: "Diagnostics & feedback",
            Targets: [count, period],
            Options:
            [
                new TweakOption("never", "Never", new Dictionary<TweakTarget, MachineValue?> { [count] = RegistryValue.DWord(0), [period] = null }),
                Option([count, period], "automatically", "Automatically", null),
            ]);
    }

    private static DeclaredTweak TailoredExperiences()
    {
        // Akari-OS turns both values off. Windows treats missing values as on.
        TweakTarget[] targets =
        [
            new(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled"),
                AbsentMeans: RegistryValue.DWord(1)),
            new(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\CPSS\Store\TailoredExperiencesWithDiagnosticDataEnabled", "Value"),
                AbsentMeans: RegistryValue.DWord(1)),
        ];

        return new DeclaredTweak(
            Id: "privacy.tailored-experiences",
            Title: "Tailored experiences",
            Description: "Lets Microsoft use your diagnostic data to show you personalised tips, ads and recommendations.",
            Category: Category.Privacy,
            Group: "Ads & suggestions",
            Targets: targets,
            Options: [DWordOption(targets, "off", "Off", 0), DWordOption(targets, "on", "On", 1)]);
    }

    private static DeclaredTweak ActivityHistory()
    {
        // Akari-OS sets the PublishUserActivities policy to 0; deleting it hands the choice back to Settings.
        var policy = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities"));

        return new DeclaredTweak(
            Id: "privacy.activity-history",
            Title: "Activity history",
            Description: "Windows keeps a history of the apps, files and websites you use on this PC.",
            Category: Category.Privacy,
            Group: "Activity & search",
            Targets: [policy],
            Options:
            [
                Option(policy, "off", "Off", RegistryValue.DWord(0)),
                Option(policy, "settings-decides", "As set in Settings", null),
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

    /// <summary>An Off/On Tweak over one or more current-user DWORDs that Windows treats as on (1) when missing.</summary>
    private static DeclaredTweak OnByDefault(
        string id, string title, string description, string group, IEnumerable<(string Key, string Name)> values,
        Activation activation = Activation.Immediately)
    {
        TweakTarget[] targets = values
            .Select(value => new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, value.Key, value.Name), AbsentMeans: RegistryValue.DWord(1)))
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

    /// <summary>A policy Tweak: "off" sets every policy value to 1; deleting them hands the choice back.</summary>
    private static DeclaredTweak OffByPolicy(
        string id, string title, string description, string group, IEnumerable<RegistryLocation> policies,
        Activation activation = Activation.Immediately)
    {
        TweakTarget[] targets = policies.Select(policy => new TweakTarget(policy)).ToArray();

        return new DeclaredTweak(
            Id: id,
            Title: title,
            Description: description,
            Category: Category.Privacy,
            Group: group,
            Targets: targets,
            Options: [DWordOption(targets, "off", "Off", 1), Option(targets, "not-blocked", "Not blocked", null)],
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

    private static DeclaredTweak AdvertisingId() =>
        // Akari-Dash writes only the Settings toggle; Winhance also sets the AdvertisingInfo policies.
        OnByDefault(
            "privacy.advertising-id",
            "Advertising ID",
            "Gives apps an ID that follows you from app to app, so they can show you ads based on what you do.",
            "Ads & suggestions",
            [(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled")]);

    private static DeclaredTweak LockScreenTips() =>
        // Akari-OS and Winhance turn RotatingLockScreenOverlayEnabled off.
        OnByDefault(
            "privacy.lock-screen-tips",
            "Fun facts and tips on the lock screen",
            "Puts fun facts, tips and promotions from Microsoft on top of the lock screen picture.",
            "Ads & suggestions",
            [(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenOverlayEnabled")]);

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

    private static DeclaredTweak OnlineSpeechRecognition()
    {
        // Winhance turns HasAccepted off. It is missing until you are asked, and missing means not accepted.
        var accepted = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted"),
            AbsentMeans: RegistryValue.DWord(0));

        return new DeclaredTweak(
            Id: "privacy.online-speech",
            Title: "Online speech recognition",
            Description: "Sends your voice to Microsoft's cloud to turn it into text, for dictation and voice typing in apps. Off keeps the on-device recognition.",
            Category: Category.Privacy,
            Group: "Speech, inking & typing",
            Targets: [accepted],
            Options: [DWordOption(accepted, "off", "Off", 0), DWordOption(accepted, "on", "On", 1)]);
    }

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

    private static DeclaredTweak AppAccess(string name, string title, string description, string? consentKey = null)
    {
        // Akari-OS and Winhance set the machine-wide ConsentStore value: "Allow" or "Deny". A matter
        // of taste, so no Recommended Option: Akari-OS itself turns camera and microphone on.
        var access = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, $@"{ConsentStore}\{consentKey ?? name}", "Value"),
            AbsentMeans: RegistryValue.String("Allow"));

        return new DeclaredTweak(
            Id: $"privacy.{name}",
            Title: title,
            Description: description,
            Category: Category.Privacy,
            Group: "App permissions",
            Targets: [access],
            Options: [Option(access, "off", "Off", RegistryValue.String("Deny")), Option(access, "on", "On", RegistryValue.String("Allow"))]);
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
