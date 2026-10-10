using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

// Privacy Tweaks, written from Akari-OS's reg.reg (MIT). Recommended Options are for performance, so
// only automatic app installs, which download and install apps in the background, has one.
public static partial class TweakCatalog
{
    private static IEnumerable<DeclaredTweak> PrivacyTweaks() =>
    [
        DiagnosticData(),
        FeedbackFrequency(),
        TailoredExperiences(),
        ActivityHistory(),
        WebSearchInStart(),
        RecentItems(),
        AutomaticAppInstalls(),
        SuggestionsInSettings(),
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
}
