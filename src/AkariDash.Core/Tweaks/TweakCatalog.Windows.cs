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
// is a Category of its own now; see TweakCatalog.Notifications.cs, and the Start, Taskbar and File
// Explorer Groups went the same way to TweakCatalog.StartMenu.cs, TweakCatalog.Taskbar.cs and
// TweakCatalog.Explorer.cs (#32).
public static partial class TweakCatalog
{
    private const string ExplorerAdvanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string ContentDelivery = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

    private static IEnumerable<DeclaredTweak> WindowsTweaks() =>
    [
        ColorMode(),

        AltTabTabs(),

        LongPaths(),
        DetailedBlueScreen(),
    ];

    private static DeclaredTweak WindowsTweak(
        string id, string title, string description, string group, TweakTarget target, IReadOnlyList<TweakOption> options, Activation activation) =>
        new(id, title, description, Category.Windows, group, [target], options, Activation: activation);

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
