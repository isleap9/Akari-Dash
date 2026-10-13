using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

// The Start menu page, the first of the three Customize Categories (#32). Written from Winhance as
// of its last MIT commit (4dc441a0; see THIRD-PARTY-NOTICES.md), Akari-OS's reg.reg (MIT) and
// AkariOS-Ultimate's Tweaks/Windows.ps1 (MIT). Every row here is a matter of taste, so none has a
// Recommended Option and "apply all recommended" skips the page entirely.
//
// Four of these rows were on the Windows page until this ticket; RenamedTweaks.Ids carries their
// saved Original Values over, so a PC that applied them before the move keeps Undo on the row.
//
// Windows reads most of them when Start next opens, and the rest when StartMenuExperienceHost
// restarts, which Akari-Dash cannot ask for: restarting someone's shell is not a Tweak. So the whole
// page says AfterSignOut, which is the first moment every row is certainly in effect.
//
// Left out, and why:
//
//   Clean Start Menu, which Winhance offers on this page, removes every pinned item. That is an
//   Action (ADR-0004): no Original Values, nothing to undo. It needs the Action machinery Akari-Dash
//   does not have yet, so it is not a row this page can honestly show.
//
//   Start_Layout — Winhance's "Start layout" with More pins and More recommendations — is gone.
//   Winhance's own MIT snapshot caps it at build 26120 ("Removed in build 26120.4250"), and the
//   machines Akari-Dash ships to are past that, so the row would write a value current Windows
//   ignores. ShowAllPins below is what replaced it.
//
//   HideRecommendedSection is Microsoft's Education-only policy, and Winhance makes it work by
//   setting IsEducationEnvironment as well — telling Windows this is a school PC, which changes far
//   more than Start. The Windows page already left it out for that reason, and the new Start menu's
//   own ShowRecentSection (the Recommended section row below) does the same job with no pretending.
//
//   HideRecentlyAddedApps, the policy pair Akari-OS also sets, is the same setting as Recently added
//   apps below. One Tweak per decision: the HKCU value is the one Settings itself writes.
//
//   Start_TrackDocs (recently opened items) stays on the Privacy page as privacy.recent-items. It
//   reaches jump lists and File Explorer too, so Start is not where it belongs.
//
//   Start's suggestions from the Microsoft Store (SubscribedContent-338388Enabled) is a
//   ContentDeliveryManager value, and that whole family — with the preset over it — lives on the
//   Privacy page. Winhance marks it Windows 10 only in any case.
//
//   Start_TrackProgs is the Windows 10 value for Most used apps; Windows 11 uses ShowFrequentList,
//   which is the row below.
//
//   The phone companion rows (RightCompanionToggledOpen, and the Companions subkey for Your Phone)
//   are left out: the subkey is per app package, so the row would only ever cover whichever
//   companion happened to be installed, and Akari-OS files them under what it cannot set natively.
public static partial class TweakCatalog
{
    private const string StartMenuKey = @"Software\Microsoft\Windows\CurrentVersion\Start";

    private static IEnumerable<DeclaredTweak> StartMenuTweaks() =>
    [
        ShowAllPins(),
        StartSection("start.pinned-section", "Pinned section", "Shows the row of apps you have pinned at the top of Start.", "ShowPinnedSection"),
        StartSection("start.recommended-section", "Recommended section", "Shows the lower half of Start, where Windows puts recently opened files and apps it suggests.", "ShowRecentSection"),
        StartSection("start.all-apps-section", "All apps list", "Shows the full list of installed apps in Start, under the pinned ones, instead of only behind the All apps button.", "ShowAllAppsSection"),
        StartMenuSize(),
        AllAppsViewMode(),
        UserInformation(),

        StartToggle("start.recently-added", "Recently added apps", "Shows apps you just installed at the top of Start's app list.", "ShowRecentList"),
        StartToggle("start.most-used", "Most used apps", "Shows the apps you open most in Start.", "ShowFrequentList"),
        StartToggle("start.suggested-files", "Recommended and suggested files", "Puts files Windows thinks you want next in Start's Recommended section, including ones from OneDrive and Office.", "ShowSuggestedFiles"),
        AdvancedToggle("start.recommendations", "Tips, shortcuts and new apps", "Shows recommendations for tips, shortcuts, new apps and more in Start.", "Start_IrisRecommendations"),
        AdvancedToggle("start.browsing-history", "Websites from your browsing history", "Puts websites you have visited in Start's Recommended section, alongside your files.", "Start_RecoPersonalizedSites"),
        AdvancedToggle("start.account-notifications", "Account notifications in Start", "Shows prompts about your Microsoft account, like backing up or finishing sign-in, on your picture in Start.", "Start_AccountNotifications"),
    ];

    /// <summary>
    /// A Show/Hide Tweak over one current-user DWORD that Windows treats as on when it is missing,
    /// which is how every value on this page ships. <paramref name="key"/> is the Start key for the
    /// values Settings' own Start page writes, and Explorer\Advanced for the older ones.
    /// </summary>
    private static DeclaredTweak StartMenuTweak(
        string id, string title, string description, string group, string key, string name, int absent = 1)
    {
        var target = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, key, name),
            AbsentMeans: RegistryValue.DWord(absent));

        return new DeclaredTweak(
            id, title, description, Category.StartMenu, group, [target],
            [DWordOption(target, "show", "Show", 1), DWordOption(target, "hide", "Hide", 0)],
            Activation: Activation.AfterSignOut);
    }

    /// <summary>One of the sections the Windows 11 Start menu is built from, in the Start key.</summary>
    private static DeclaredTweak StartSection(string id, string title, string description, string name) =>
        StartMenuTweak(id, title, description, "Layout", StartMenuKey, name);

    /// <summary>One of the lists Start can show, in the Start key.</summary>
    private static DeclaredTweak StartToggle(string id, string title, string description, string name) =>
        StartMenuTweak(id, title, description, "What Start shows", StartMenuKey, name);

    /// <summary>One of the older Start settings, which live in Explorer\Advanced rather than Start.</summary>
    private static DeclaredTweak AdvancedToggle(string id, string title, string description, string name) =>
        StartMenuTweak(id, title, description, "What Start shows", ExplorerAdvanced, name);

    private static DeclaredTweak ShowAllPins()
    {
        // Winhance's replacement for Start_Layout, for build 26120.4250 and later: ShowAllPinsList
        // opens Start on the full pin list rather than the pinned section. Its MIT snapshot gives 1
        // as the value Windows behaves as when it is missing.
        var target = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, StartMenuKey, "ShowAllPinsList"),
            AbsentMeans: RegistryValue.DWord(1));

        return new DeclaredTweak(
            "start.all-pins",
            "Show all pins by default",
            "Opens Start with every pinned app already showing, instead of the few that fit above All apps. Windows added this in build 26120.4250; on an earlier build the value is written and ignored.",
            Category.StartMenu, "Layout", [target],
            [DWordOption(target, "on", "On", 1), DWordOption(target, "off", "Off", 0)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak UserInformation()
    {
        // Akari-OS and AkariOS-Ultimate both set HideUserInformation to 0, which is where Windows
        // ships: your name and picture show at the bottom of Start.
        var target = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, StartMenuKey, "HideUserInformation"),
            AbsentMeans: RegistryValue.DWord(0));

        return new DeclaredTweak(
            "start.user-information",
            "Your name and picture",
            "Shows your account name and picture at the bottom of Start, which is also where the sign-out and lock menu lives.",
            Category.StartMenu, "Layout", [target],
            [DWordOption(target, "show", "Show", 0), DWordOption(target, "hide", "Hide", 1)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak StartMenuSize()
    {
        // Akari-OS sets StartMenuSize to 1 for the small Start menu and documents no other value, so
        // the second Option deletes it rather than guessing what 0 or 2 mean: an absent value is
        // whatever Windows does by itself, which is the honest other half of this choice.
        var size = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, StartMenuKey, "StartMenuSize"));

        return new DeclaredTweak(
            "start.size",
            "Start menu size",
            "Makes Start the small size Akari-OS uses, which shows fewer pins and takes less of the screen. Windows default hands the size back to Windows, which picks it from the screen and your pins.",
            Category.StartMenu, "Layout", [size],
            [
                Option(size, "small", "Small", RegistryValue.DWord(1)),
                Option(size, "windows-default", "Windows default", null),
            ],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak AllAppsViewMode()
    {
        // Akari-OS sets AllAppsViewMode to 2 for the plain list and documents no other value, so the
        // other Option deletes it, as with the size above.
        var mode = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, StartMenuKey, "AllAppsViewMode"));

        return new DeclaredTweak(
            "start.all-apps-view",
            "How All apps is laid out",
            "Shows All apps as one plain alphabetical list, the way Akari-OS sets it, instead of the grid and category view Windows chooses by itself.",
            Category.StartMenu, "Layout", [mode],
            [
                Option(mode, "list", "List", RegistryValue.DWord(2)),
                Option(mode, "windows-default", "Windows default", null),
            ],
            Activation: Activation.AfterSignOut);
    }
}
