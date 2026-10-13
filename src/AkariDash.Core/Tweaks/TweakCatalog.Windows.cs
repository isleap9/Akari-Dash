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
        BlackTheme(),

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

    private static DeclaredTweak BlackTheme()
    {
        // AkariOS-Ultimate's "Theme black" (MIT, Tweaks/Windows.ps1), with its own revert as the other
        // Option: dark mode, transparency off, and the accent palette that Start, the taskbar and title
        // bars are painted from set to near-black, from which Windows derives black. Undo restores what the
        // PC actually had; "Windows default" is the source's revert, which is Windows' stock blue.
        //
        // A row of its own rather than a third Option on Color mode: that row's two values are the
        // first two here, so they share them (and the engine reads one applied over the other as
        // Custom, not Drift), while growing Color mode to nine targets would turn every PC that
        // already applied "Dark" into Drift.
        //
        // Left out of the port: Control Panel\Colors\Background. AkariOS writes "0 0 0" in both
        // directions, so it is not part of the choice, and a PC with any other desktop colour would
        // read Custom on both Options for no reason.
        //
        // Also left out: DWM's AccentColor, ColorizationColor and ColorizationAfterglow, which AkariOS
        // writes too. Windows works those three out for itself from the accent palette when the theme
        // loads, and overwrites whatever was there: in the VM on 2026-10-13, after a sign-out, Black
        // read as Drift with AccentColor = 0 and both colorization values = 0xC4000000 — black, derived
        // from the near-black palette, and exactly the look intended. A value Windows always rewrites
        // is not one Akari-Dash can hold, so it is not a target. A PC that applied Black before this
        // still has them in its record, and Undo still puts them back.
        const string personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string accent = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
        const string dwm = @"Software\Microsoft\Windows\DWM";

        TweakTarget User(string key, string name, int? absent = null) => new(
            new RegistryLocation(RegistryHive.CurrentUser, key, name),
            AbsentMeans: absent is { } value ? RegistryValue.DWord(value) : null);

        // The first two are exactly Color mode's targets, so the two rows share them.
        TweakTarget[] targets =
        [
            User(personalize, "AppsUseLightTheme", 1),
            User(personalize, "SystemUsesLightTheme", 1),
            User(personalize, "ColorPrevalence", 0),
            User(personalize, "EnableTransparency", 1),
            new(new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme")),
            User(accent, "AccentPalette"),
            User(accent, "StartColorMenu"),
            User(accent, "AccentColorMenu"),
            User(dwm, "EnableWindowColorization", 0),
        ];

        return new DeclaredTweak(
            "windows.black-theme",
            "Black theme (AkariOS-Ultimate)",
            "Dark mode with Start, the taskbar and window title bars painted near-black instead of in an accent colour, and transparency off. On an OLED monitor darker is less power and less wear on the taskbar, which never moves; it is not true black, because Windows draws its dark surfaces a shade above it. Windows default puts back the light theme and Windows' stock blue accent.",
            Category.Windows, "Appearance", targets,
            [
                Theme("default", "Windows default", appsLight: 1, prevalence: 0, transparency: 1, machineAppsLight: null,
                    palette: [0x99, 0xeb, 0xff, 0x00, 0x4c, 0xc2, 0xff, 0x00, 0x00, 0x91, 0xf8, 0x00, 0x00, 0x78, 0xd4, 0x00,
                              0x00, 0x67, 0xc0, 0x00, 0x00, 0x3e, 0x92, 0x00, 0x00, 0x1a, 0x68, 0x00, 0xf7, 0x63, 0x0c, 0x00],
                    startMenu: unchecked((int)0xffc06700), accentMenu: unchecked((int)0xffd47800), colorization: 0),
                Theme("black", "Black", appsLight: 0, prevalence: 1, transparency: 0, machineAppsLight: 0,
                    palette: [0x64, 0x64, 0x64, 0x00, 0x6b, 0x6b, 0x6b, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                              0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
                    startMenu: 0, accentMenu: 0, colorization: 1),
            ],
            Activation: Activation.AfterSignOut);

        TweakOption Theme(
            string id, string label, int appsLight, int prevalence, int transparency, int? machineAppsLight, byte[] palette,
            int startMenu, int accentMenu, int colorization) =>
            new(id, label, new Dictionary<TweakTarget, MachineValue?>
            {
                [targets[0]] = RegistryValue.DWord(appsLight),
                [targets[1]] = RegistryValue.DWord(appsLight),
                [targets[2]] = RegistryValue.DWord(prevalence),
                [targets[3]] = RegistryValue.DWord(transparency),
                [targets[4]] = machineAppsLight is { } machine ? RegistryValue.DWord(machine) : null,
                [targets[5]] = new RegistryValue(RegistryValueKind.Binary, palette),
                [targets[6]] = RegistryValue.DWord(startMenu),
                [targets[7]] = RegistryValue.DWord(accentMenu),
                [targets[8]] = RegistryValue.DWord(colorization),
            });
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
