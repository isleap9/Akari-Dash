using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

// The Taskbar page, the second of the three Customize Categories (#32). Written from Winhance as of
// its last MIT commit (4dc441a0; see THIRD-PARTY-NOTICES.md), Akari-OS's reg.reg (MIT) and
// AkariOS-Ultimate's Tweaks/Windows.ps1 (MIT). Taste again, so nothing here has a Recommended
// Option.
//
// Five of these rows were on the Windows page until this ticket; RenamedTweaks.Ids carries their
// saved Original Values over. One of the five is also fixed: taskbar.search had the values for
// "Icon and label" and "Search box" the wrong way round, and so had the wrong AbsentMeans — see
// SearchOnTaskbar below.
//
// Left out, and why:
//
//   Clean Taskbar, Winhance's unpin-everything, is an Action (ADR-0004), not a Tweak: nothing to
//   undo. Same reason as Clean Start Menu.
//
//   ShowCopilotButton, which the Privacy page's Copilot comment left for this ticket, does not
//   belong here after all. Winhance's MIT snapshot scopes it to builds 22621-26099 — Windows 11
//   22H2 and 23H2 — and current Windows has no Copilot button on the taskbar to hide. A row that
//   writes a value Windows ignores is worse than no row. privacy.copilot still turns the Copilot
//   pane off, which is the setting that does something.
//
//   Meet Now (HideSCAMeetNow), News and interests (EnableFeeds), small taskbar icons
//   (TaskbarSmallIcons) and the transparent taskbar (TaskbarAcrylicOpacity) are all marked Windows
//   10 only by the snapshot they come from, and Windows 11 has none of those features.
//
//   EnableAutoTray, "always show all system tray icons", stays out: it made no visible difference in
//   the VM on build 26300 when the Windows page was written, and nothing since has changed that.
//
//   IconSizePreference ("show smaller taskbar buttons") and the taskbar's Resume button, which
//   Akari-OS writes as a value plainly named IsEnabled under Explorer\Advanced, have no documented
//   name or value set. A row over a value called IsEnabled, with no way to check what else reads it,
//   is not a row we can stand behind.
//
//   TaskbarAnimations is a visual effect and belongs with the others, not here.
public static partial class TweakCatalog
{
    private const string TaskbarSearchKey = @"Software\Microsoft\Windows\CurrentVersion\Search";

    private static IEnumerable<DeclaredTweak> TaskbarTweaks() =>
    [
        SearchOnTaskbar(),
        TaskbarButton("taskbar.task-view", "Task View button", "Shows the Task View button, for your open windows and desktops, on the taskbar.", "ShowTaskViewButton"),
        TaskbarButton("taskbar.chat", "Chat button", "Shows the Chat button, which opens Microsoft Teams, on the taskbar.", "TaskbarMn", absent: 1),
        Widgets(),

        TaskbarAlignment(),
        ShowDesktopCorner(),
        ShareFromTaskbar(),
        EndTask(),
    ];

    /// <summary>
    /// A Show/Hide Tweak over one of the taskbar's own DWORDs in Explorer\Advanced. Explorer reads
    /// these as it goes, but only reliably once it has restarted, which a sign-out guarantees.
    /// </summary>
    private static DeclaredTweak TaskbarTweak(
        string id, string title, string description, string group, string name, int shownValue, int hiddenValue, int absent,
        string shownLabel = "Show", string hiddenLabel = "Hide")
    {
        var target = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, ExplorerAdvanced, name),
            AbsentMeans: RegistryValue.DWord(absent));

        return new DeclaredTweak(
            id, title, description, Category.Taskbar, group, [target],
            [DWordOption(target, "show", shownLabel, shownValue), DWordOption(target, "hide", hiddenLabel, hiddenValue)],
            Activation: Activation.AfterSignOut);
    }

    /// <summary>One of the buttons the taskbar can show, where 1 shows it and 0 hides it.</summary>
    private static DeclaredTweak TaskbarButton(string id, string title, string description, string name, int absent = 1) =>
        TaskbarTweak(id, title, description, "What the taskbar shows", name, shownValue: 1, hiddenValue: 0, absent: absent);

    private static DeclaredTweak SearchOnTaskbar()
    {
        // Akari-OS and AkariOS-Ultimate both hide search (0). The four modes are Microsoft's own, as
        // its ConfigureSearchOnTaskbarMode policy documents them and as Winhance's MIT snapshot has
        // them: 0 hidden, 1 the icon, 2 the icon with a label, 3 the search box. Windows 11 ships
        // with the search box, so a missing value counts as 3.
        //
        // The Windows page had 2 and 3 swapped, and so said a missing value meant 2. That made a
        // stock machine read "Search box" from a value it did not have while the taskbar showed the
        // box for a different reason, and picking "Search box" wrote the icon and label instead.
        var mode = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, TaskbarSearchKey, "SearchboxTaskbarMode"),
            AbsentMeans: RegistryValue.DWord(3));

        return new DeclaredTweak(
            "taskbar.search",
            "Search on the taskbar",
            "How search shows on the taskbar. You can still search by opening Start and typing.",
            Category.Taskbar, "What the taskbar shows", [mode],
            [
                DWordOption(mode, "hidden", "Hidden", 0),
                DWordOption(mode, "icon", "Icon only", 1),
                DWordOption(mode, "icon-label", "Icon and label", 2),
                DWordOption(mode, "box", "Search box (Windows default)", 3),
            ],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak Widgets()
    {
        // Akari-OS sets the AllowNewsAndInterests policy to 0; deleting it gives widgets back. It is
        // the machine policy Microsoft documents: Winhance writes a current-user copy of it too, but
        // the documented one is HKLM and that is the one Windows reads.
        var policy = new TweakTarget(new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests"));

        return new DeclaredTweak(
            "taskbar.widgets",
            "Widgets",
            "The widgets board with news, weather and more, and its button on the taskbar.",
            Category.Taskbar, "What the taskbar shows", [policy],
            [Option(policy, "off", "Off", RegistryValue.DWord(0)), Option(policy, "not-blocked", "Not blocked", null)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak TaskbarAlignment()
    {
        // Akari-OS sets TaskbarAl to 0 (left); Windows 11 centres the taskbar when it is missing.
        var alignment = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarAl"),
            AbsentMeans: RegistryValue.DWord(1));

        return new DeclaredTweak(
            "taskbar.alignment",
            "Taskbar alignment",
            "Where the Start button and app icons sit on the taskbar.",
            Category.Taskbar, "Taskbar behaviour", [alignment],
            [DWordOption(alignment, "center", "Center", 1), DWordOption(alignment, "left", "Left", 0)],
            Activation: Activation.Immediately);
    }

    private static DeclaredTweak ShowDesktopCorner() =>
        // Akari-OS and AkariOS-Ultimate set TaskbarSd to 1. Windows 11 ships with it on, so a missing
        // value counts as on.
        TaskbarTweak(
            "taskbar.show-desktop-corner",
            "Show desktop button in the corner",
            "Keeps the sliver at the far end of the taskbar that minimises every window when you click it, which is easy to hit by accident when reaching for the clock.",
            "Taskbar behaviour", "TaskbarSd", shownValue: 1, hiddenValue: 0, absent: 1,
            shownLabel: "On", hiddenLabel: "Off");

    private static DeclaredTweak ShareFromTaskbar() =>
        // Akari-OS and AkariOS-Ultimate set TaskbarSn to 0: the "share any window from my taskbar"
        // choice in Settings. Windows ships with it on.
        TaskbarTweak(
            "taskbar.share-window",
            "Share a window from the taskbar",
            "Puts a Share button on an app's taskbar preview, so a window can be shared into a call straight from there.",
            "Taskbar behaviour", "TaskbarSn", shownValue: 1, hiddenValue: 0, absent: 1,
            shownLabel: "On", hiddenLabel: "Off");

    private static DeclaredTweak EndTask()
    {
        // Winhance and Akari-OS both set TaskbarEndTask; Windows treats a missing value as off.
        var endTask = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, $@"{ExplorerAdvanced}\TaskbarDeveloperSettings", "TaskbarEndTask"),
            AbsentMeans: RegistryValue.DWord(0));

        return new DeclaredTweak(
            "taskbar.end-task",
            "End task in the taskbar menu",
            "Adds End task to an app's right-click menu on the taskbar, to close an app that stopped responding.",
            Category.Taskbar, "Taskbar behaviour", [endTask],
            [DWordOption(endTask, "on", "On", 1), DWordOption(endTask, "off", "Off", 0)],
            Activation: Activation.AfterSignOut);
    }
}
