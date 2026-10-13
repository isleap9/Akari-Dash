using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

// Windows Tweaks: how Windows looks and behaves. Written from Akari-OS's reg.reg (MIT) and Winhance
// as of its last MIT commit (4dc441a0; see THIRD-PARTY-NOTICES.md). All are a matter of taste, so
// none has a Recommended Option.
//
// Left out: the classic right-click menu works by a registry key existing, and Undo can only delete
// the value it wrote, which leaves the key and so the classic menu behind. Hiding Start's Recommended
// section needs the device to pretend to be a school PC (IsEducationEnvironment), which changes more
// than Start. The Copilot taskbar button is gone in current Windows; privacy.copilot covers Copilot.
// EnableAutoTray (show all notification area icons), which Akari-OS sets, made no visible difference
// in the VM on build 26300, so it is left out too. The Notifications Group that used to be here
// is a Category of its own now; see TweakCatalog.Notifications.cs, and the Start and Taskbar Groups
// went the same way to TweakCatalog.StartMenu.cs and TweakCatalog.Taskbar.cs (#32).
public static partial class TweakCatalog
{
    private const string ExplorerKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer";
    private const string ExplorerAdvanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string ContentDelivery = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

    private static IEnumerable<DeclaredTweak> WindowsTweaks() =>
    [
        Toggle("windows.file-extensions", "File name extensions", "Shows the end of file names, like .exe or .pdf, so you can tell what kind of file it is.",
            "File Explorer", ExplorerAdvanced, "HideFileExt", shownValue: 0, hiddenValue: 1, absent: 1),
        Toggle("windows.hidden-files", "Hidden files and folders", "Shows files and folders that are marked hidden, such as AppData.",
            "File Explorer", ExplorerAdvanced, "Hidden", shownValue: 1, hiddenValue: 2, absent: 2),
        OpenExplorerTo(),
        Toggle("windows.full-path", "Full path in the title bar", "Shows the whole folder path, like C:\\Users\\You\\Documents, at the top of File Explorer.",
            "File Explorer", $@"{ExplorerKey}\CabinetState", "FullPath", shownValue: 1, hiddenValue: 0, absent: 0),
        Toggle("windows.frequent-folders", "Frequent folders", "Shows the folders you open most in File Explorer's Home and Quick access.",
            "File Explorer", ExplorerKey, "ShowFrequent", shownValue: 1, hiddenValue: 0, absent: 1),
        Toggle("windows.recent-files", "Recent files", "Shows the files you opened recently in File Explorer's Home.",
            "File Explorer", ExplorerKey, "ShowRecent", shownValue: 1, hiddenValue: 0, absent: 1),

        ColorMode(),

        AltTabTabs(),

        LongPaths(),
        DetailedBlueScreen(),
    ];

    /// <summary>A Show/Hide Tweak over one current-user DWORD; Explorer reads these when you sign in.</summary>
    private static DeclaredTweak Toggle(
        string id, string title, string description, string group, string key, string name, int shownValue, int hiddenValue, int absent)
    {
        var target = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, key, name), AbsentMeans: RegistryValue.DWord(absent));

        return WindowsTweak(id, title, description, group, target,
            [DWordOption(target, "show", "Show", shownValue), DWordOption(target, "hide", "Hide", hiddenValue)],
            Activation.AfterSignOut);
    }

    private static DeclaredTweak WindowsTweak(
        string id, string title, string description, string group, TweakTarget target, IReadOnlyList<TweakOption> options, Activation activation) =>
        new(id, title, description, Category.Windows, group, [target], options, Activation: activation);

    private static DeclaredTweak OpenExplorerTo()
    {
        // Akari-OS opens to This PC (1); Windows opens to Home (2) when LaunchTo is missing.
        var launchTo = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, ExplorerAdvanced, "LaunchTo"), AbsentMeans: RegistryValue.DWord(2));

        return WindowsTweak("windows.open-explorer-to", "Open File Explorer to", "Which page File Explorer shows when you open it.",
            "File Explorer", launchTo,
            [DWordOption(launchTo, "home", "Home", 2), DWordOption(launchTo, "this-pc", "This PC", 1)],
            Activation.Immediately);
    }

    private static DeclaredTweak AltTabTabs()
    {
        // MultiTaskingAltTabFilter, the Settings > Multitasking choice (NinjaOne, ElevenForum): 0 the 20
        // most recent tabs, 1 five, 2 three (Windows' default, also when missing), 3 no tabs.
        var filter = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, ExplorerAdvanced, "MultiTaskingAltTabFilter"),
            AbsentMeans: RegistryValue.DWord(2));

        return WindowsTweak("windows.alt-tab-tabs", "Tabs in Alt+Tab", "Whether Alt+Tab and Snap show the open tabs of apps such as Edge, next to their windows.",
            "Multitasking", filter,
            [
                DWordOption(filter, "none", "Windows only, no tabs", 3),
                DWordOption(filter, "three", "3 most recent tabs (Windows default)", 2),
                DWordOption(filter, "five", "5 most recent tabs", 1),
                DWordOption(filter, "twenty", "20 most recent tabs", 0),
            ],
            Activation.Immediately);
    }

    private static DeclaredTweak ColorMode()
    {
        // Akari-OS sets both to 0 (dark). Windows uses light mode when they are missing.
        TweakTarget[] targets =
        [
            new(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme"), AbsentMeans: RegistryValue.DWord(1)),
            new(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme"), AbsentMeans: RegistryValue.DWord(1)),
        ];

        return new DeclaredTweak("windows.color-mode", "Color mode", "Light or dark colours for Windows and for apps that follow it.",
            Category.Windows, "Appearance", targets,
            [DWordOption(targets, "light", "Light", 1), DWordOption(targets, "dark", "Dark", 0)]);
    }

    private static DeclaredTweak LongPaths()
    {
        // Akari-OS and Winhance set LongPathsEnabled; Windows treats a missing value as off.
        var enabled = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\FileSystem", "LongPathsEnabled"),
            AbsentMeans: RegistryValue.DWord(0));

        return WindowsTweak("windows.long-paths", "Long file paths", "Lets apps that support it use file paths longer than 260 characters.",
            "System", enabled,
            [DWordOption(enabled, "off", "Off", 0), DWordOption(enabled, "on", "On", 1)],
            Activation.AfterRestart);
    }

    private static DeclaredTweak DetailedBlueScreen()
    {
        // Akari-OS sets DisplayParameters to 1. Windows treats a missing value as off.
        var details = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\CrashControl", "DisplayParameters"),
            AbsentMeans: RegistryValue.DWord(0));

        return WindowsTweak("windows.detailed-blue-screen", "Details on the blue screen", "Shows the technical error details on the blue screen when Windows crashes, which helps find the cause.",
            "System", details,
            [DWordOption(details, "off", "Off", 0), DWordOption(details, "on", "On", 1)],
            Activation.AfterRestart);
    }
}
