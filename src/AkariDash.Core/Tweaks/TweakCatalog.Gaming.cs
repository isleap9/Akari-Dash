using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

// Gaming Tweaks, written from Akari-OS (MIT), AkariOS-Ultimate (MIT; processor scheduling, exclusive
// fullscreen and the Windows defaults it restores), Winhance as of its last MIT commit (4dc441a0; GPU
// priority, NVIDIA sharpening, Game Bar tips, prefetch, the extra visual effects and accessibility
// shortcuts), public write-ups of the wallpaper quality value and the WebView2 search feature override
// (Winhance's newer "Other" section lists both, but nothing is taken from that version) and Microsoft's
// documentation of the Multimedia Class Scheduler and the Storage Sense policy.
//
// Left out from Winhance's Gaming page: its VRR switch writes a VRROptimizeEnable DWORD, but Windows
// keeps that setting inside DirectXUserGlobalSettings (see WindowedGameOptimizations). Desktop
// composition cannot be turned off since Windows 8, so CompositionPolicy does nothing.
// ServicesPipeTimeout's "faster" 30 seconds is already Windows' default. Nagle's algorithm is set per
// network adapter, so it waits for the per-adapter work that DNS needs. The UserPreferencesMask
// effects need a way to change one bit of a value.
public static partial class TweakCatalog
{
    private static IEnumerable<DeclaredTweak> GamingTweaks() =>
    [
        GameMode(),
        GameCaptures(),
        BackgroundRecording(),
        GameBarControllerButton(),
        GameBarTips(),
        HardwareGpuScheduling(),
        NvidiaSharpening(),
        WindowedGameOptimizations(),
        FullscreenOptimizations(),
        ProcessorScheduling(),
        SvcHostSplit(),
        PowerThrottling(),
        SystemResponsiveness(),
        GamesPriority(),
        GamesGpuPriority(),
        NetworkThrottling(),
        BackgroundApps(),
        AutomaticMaintenance(),
        StorageSense(),
        StartupDelay(),
        Prefetch(),
        SearchWebView2(),
        MenuShowDelay(),
        WindowAnimations(),
        TaskbarAnimations(),
        Peek(),
        Transparency(),
        DragFullWindows(),
        ThumbnailsInsteadOfIcons(),
        TranslucentSelection(),
        IconLabelShadows(),
        SaveTaskbarThumbnails(),
        FontSmoothing(),
        WallpaperQuality(),
        MouseAcceleration(),
    ];

    // Listed after the Scheduled tasks (TweakCatalog.cs), so Accessibility is the last section on the
    // Gaming page, as in Winhance.
    private static IEnumerable<DeclaredTweak> AccessibilityTweaks() =>
    [
        StickyKeysShortcut(),
        AccessibilityShortcut("gaming.filter-keys-shortcut", "Filter Keys shortcut", "Holding the right Shift key for eight seconds turns on Filter Keys, which ignores quick or repeated key presses.", "Keyboard Response", "126", "2"),
        AccessibilityShortcut("gaming.toggle-keys-shortcut", "Toggle Keys shortcut", "Holding Num Lock for five seconds turns on Toggle Keys, which beeps whenever Caps Lock, Num Lock or Scroll Lock is pressed.", "ToggleKeys", "62", "34"),
        AccessibilityShortcut("gaming.mouse-keys-shortcut", "Mouse Keys shortcut", "Left Alt + left Shift + Num Lock turns on Mouse Keys, which moves the pointer with the number pad.", "MouseKeys", "62", "130"),
        AccessibilityShortcut("gaming.high-contrast-shortcut", "High Contrast shortcut", "Left Alt + left Shift + Print Screen switches the screen to high contrast colours.", "HighContrast", "126", "4194"),
        NarratorShortcut(),
    ];

    private static DeclaredTweak GameMode()
    {
        // Windows treats a missing value as Game Mode on.
        var enabled = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled"),
            AbsentMeans: RegistryValue.DWord(1));
        var on = DWordOption(enabled, "on", "On", 1);

        return new DeclaredTweak(
            Id: "gaming.game-mode",
            Title: "Game Mode",
            Description: "Lets Windows prioritise the game you are playing, for example by holding back Windows Update driver installs and restart notifications while it runs.",
            Category: Category.Gaming,
            Group: "Game Mode",
            Targets: [enabled],
            Options: [DWordOption(enabled, "off", "Off", 0), on],
            Recommended: on);
    }

    private static DeclaredTweak GameCaptures()
    {
        // Akari-OS turns both off. Windows treats missing values as captures allowed. No Recommended
        // Option: while nothing is being recorded, captures cost next to nothing, and turning them
        // off takes away a feature.
        TweakTarget[] targets =
        [
            new(new RegistryLocation(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled"),
                AbsentMeans: RegistryValue.DWord(1)),
            new(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled"),
                AbsentMeans: RegistryValue.DWord(1)),
        ];

        return new DeclaredTweak(
            Id: "gaming.game-captures",
            Title: "Game captures",
            Description: "Lets Game Bar take screenshots and record clips of your games. Turn it off if you use your graphics driver's or another app's recorder instead.",
            Category: Category.Gaming,
            Group: "Game Bar",
            Targets: targets,
            Options: [DWordOption(targets, "off", "Off", 0), DWordOption(targets, "on", "On", 1)]);
    }

    private static DeclaredTweak BackgroundRecording()
    {
        // Akari-OS turns this off; it is also off on a fresh install (missing value).
        var historical = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "HistoricalCaptureEnabled"),
            AbsentMeans: RegistryValue.DWord(0));
        var off = DWordOption(historical, "off", "Off", 0);

        return new DeclaredTweak(
            Id: "gaming.background-recording",
            Title: "Record what happened",
            Description: "Game Bar keeps recording the last few minutes of your game so you can save a clip afterwards. The constant video encoding costs frame rate.",
            Category: Category.Gaming,
            Group: "Game Bar",
            Targets: [historical],
            Options: [off, DWordOption(historical, "on", "On", 1)],
            Recommended: off);
    }

    private static DeclaredTweak GameBarControllerButton()
    {
        // Akari-OS turns this off. No Recommended Option: it only decides what the Xbox button does.
        var nexus = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled"),
            AbsentMeans: RegistryValue.DWord(1));

        return new DeclaredTweak(
            Id: "gaming.game-bar-controller-button",
            Title: "Open Game Bar with the controller",
            Description: "Pressing the Xbox button on a controller opens Game Bar. Turn it off if the overlay keeps popping up over your games.",
            Category: Category.Gaming,
            Group: "Game Bar",
            Targets: [nexus],
            Options: [DWordOption(nexus, "off", "Off", 0), DWordOption(nexus, "on", "On", 1)]);
    }

    private static DeclaredTweak HardwareGpuScheduling()
    {
        // Akari-OS turns this on (HwSchMode 2). What a missing value means depends on the graphics
        // card, driver and Windows version, so "Let Windows decide" deletes it instead of guessing.
        var hwSchMode = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode"));
        var on = DWordOption(hwSchMode, "on", "On", 2);

        return new DeclaredTweak(
            Id: "gaming.hardware-gpu-scheduling",
            Title: "Hardware-accelerated GPU scheduling",
            Description: "Lets the graphics card manage its own memory and work queue instead of Windows, which can lower latency and CPU load in games. It needs a graphics card and driver that support it; otherwise nothing changes.",
            Category: Category.Gaming,
            Group: "Graphics",
            Targets: [hwSchMode],
            Options: [on, DWordOption(hwSchMode, "off", "Off", 1), Option(hwSchMode, "windows-decides", "Let Windows decide", null)],
            Recommended: on,
            Activation: Activation.AfterRestart);
    }

    private static DeclaredTweak ProcessorScheduling()
    {
        // Win32PrioritySeparation packs three 2-bit fields: quantum length (bits 4-5), quantum
        // type (bits 2-3) and foreground boost (bits 0-1). Every combination is offered, labelled
        // as in AkariOS-Ultimate's tuner (MIT); 0x26 is Akari-OS's "best performance of programs".
        // A fresh install has 0x02, where "Default" lets Windows choose (short, variable on desktops).
        var separation = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation"));

        string[] lengths = ["Default", "Long", "Short"];
        string[] types = ["Default", "Variable", "Fixed"];
        string[] boosts = ["None", "Medium", "High"];

        var options = (
            from length in Enumerable.Range(0, 3)
            from type in Enumerable.Range(0, 3)
            from boost in Enumerable.Range(0, 3)
            let value = (length << 4) | (type << 2) | boost
            select DWordOption(separation, $"0x{value:X2}", $"{lengths[length]} · {types[type]} · {boosts[boost]} (0x{value:X2})", value))
            .ToList();

        return new DeclaredTweak(
            Id: "gaming.processor-scheduling",
            Title: "Processor scheduling",
            Description: "How long each app gets the CPU at a time, and how much the window in front, such as your game, is favoured over everything in the background.",
            Category: Category.Gaming,
            Group: "Processor",
            Targets: [separation],
            Options: options,
            Recommended: options.Single(option => option.Id == "0x26"));
    }

    private static DeclaredTweak PowerThrottling()
    {
        // Akari-OS sets PowerThrottlingOff to 1. A missing value means power throttling is on.
        var throttlingOff = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff"),
            AbsentMeans: RegistryValue.DWord(0));
        var off = DWordOption(throttlingOff, "off", "Off", 1);

        return new DeclaredTweak(
            Id: "gaming.power-throttling",
            Title: "Power throttling",
            Description: "Windows runs apps it thinks are in the background on slower, power-saving CPU settings. It can misjudge launchers, voice chat or the game itself; turning it off keeps everything at full speed, at the cost of battery life on laptops.",
            Category: Category.Gaming,
            Group: "Processor",
            Targets: [throttlingOff],
            Options: [off, DWordOption(throttlingOff, "on", "On", 0)],
            Recommended: off,
            Activation: Activation.AfterRestart);
    }

    private static DeclaredTweak BackgroundApps()
    {
        // Akari-OS sets the "Let Windows apps run in the background" policy to Force Deny (2).
        // No Recommended Option: Store apps then stop sending notifications, syncing and ringing
        // alarms. Deleting the policy hands the choice back to each app's own setting.
        var policy = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground"));

        return new DeclaredTweak(
            Id: "gaming.background-apps",
            Title: "Store apps in the background",
            Description: "Whether apps from the Microsoft Store may keep running when you are not using them. Blocking them frees a little memory and CPU, but they can no longer show notifications or sync until you open them.",
            Category: Category.Gaming,
            Group: "Background activity",
            Targets: [policy],
            Options: [Option(policy, "block", "Block", RegistryValue.DWord(2)), Option(policy, "per-app", "Each app decides", null)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak MouseAcceleration()
    {
        // "Enhance pointer precision". Akari-OS sets all three values to "0"; 1/6/10 are the Windows
        // defaults, also used when they are missing. No Recommended Option: it changes how aiming
        // feels, not how fast the game runs, and most games read raw mouse input anyway.
        TweakTarget[] targets = [MouseTarget("MouseSpeed", "1"), MouseTarget("MouseThreshold1", "6"), MouseTarget("MouseThreshold2", "10")];

        return new DeclaredTweak(
            Id: "gaming.mouse-acceleration",
            Title: "Enhance pointer precision",
            Description: "Mouse acceleration: the pointer travels further when you move the mouse quickly. Turning it off makes the same hand movement always move the pointer the same distance.",
            Category: Category.Gaming,
            Group: "Input",
            Targets: targets,
            Options:
            [
                Option(targets, "off", "Off", RegistryValue.String("0")),
                AbsentMeansOption(targets, "on", "On"),
            ],
            Activation: Activation.AfterSignOut);

        static TweakTarget MouseTarget(string name, string windowsDefault) => new(
            new RegistryLocation(RegistryHive.CurrentUser, @"Control Panel\Mouse", name),
            AbsentMeans: RegistryValue.String(windowsDefault));
    }

    private static DeclaredTweak StickyKeysShortcut()
    {
        // Akari-OS sets the StickyKeys flags to "2" (shortcut off); "510" is the Windows default
        // (shortcut and its prompt on). No Recommended Option: it is about stray keypresses, not speed.
        var flags = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Control Panel\Accessibility\StickyKeys", "Flags"),
            AbsentMeans: RegistryValue.String("510"));

        return new DeclaredTweak(
            Id: "gaming.sticky-keys-shortcut",
            Title: "Sticky Keys shortcut",
            Description: "Pressing Shift five times asks whether to turn on Sticky Keys, which can pull you out of a game that uses Shift a lot.",
            Category: Category.Gaming,
            Group: "Accessibility",
            Targets: [flags],
            Options: [Option(flags, "off", "Off", RegistryValue.String("2")), Option(flags, "on", "On", RegistryValue.String("510"))],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak WindowedGameOptimizations()
    {
        // Akari-OS sets SwapEffectUpgradeEnable=1. The value is one string holding every per-user
        // DirectX graphics setting (Auto HDR too), so an Option writes the whole string and Undo puts
        // back what it held. Akari-OS also writes VRROptimizeEnable=0; that is left out so "On" changes
        // only what it says. What a missing value means varies by Windows version, so "Let Windows
        // decide" deletes it. No Recommended Option, because of that shared string.
        var settings = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings"));

        return new DeclaredTweak(
            Id: "gaming.windowed-game-optimizations",
            Title: "Optimizations for windowed games",
            Description: "Lets older games in a window or borderless window use the faster flip presentation model, which lowers latency much like exclusive fullscreen. Windows keeps this and Auto HDR in one setting, so choosing here also sets Auto HDR back to its default while this is applied.",
            Category: Category.Gaming,
            Group: "Graphics",
            Targets: [settings],
            Options:
            [
                Option(settings, "on", "On", RegistryValue.String("SwapEffectUpgradeEnable=1;")),
                Option(settings, "off", "Off", RegistryValue.String("SwapEffectUpgradeEnable=0;")),
                Option(settings, "windows-decides", "Let Windows decide", null),
            ]);
    }

    private static DeclaredTweak FullscreenOptimizations()
    {
        // AkariOS-Ultimate's "hardware legacy flip" values ask for true exclusive fullscreen; its
        // revert (0, deleted, 0, 0) is Windows' own fullscreen optimizations, which missing values
        // also count as. No Recommended Option: DirectX 12 games have no exclusive fullscreen, and
        // which works better depends on the game.
        TweakTarget[] targets =
        [
            ConfigStore("GameDVR_FSEBehaviorMode", 0),
            ConfigStore("GameDVR_FSEBehavior", null),
            ConfigStore("GameDVR_HonorUserFSEBehaviorMode", 0),
            ConfigStore("GameDVR_DXGIHonorFSEWindowsCompatible", 0),
        ];

        return new DeclaredTweak(
            Id: "gaming.fullscreen-optimizations",
            Title: "Fullscreen optimizations",
            Description: "Windows runs fullscreen games as a special borderless window so overlays and Alt+Tab work smoothly. Turning it off gives DirectX 9 to 11 games true exclusive fullscreen, which some players find lower in latency.",
            Category: Category.Gaming,
            Group: "Graphics",
            Targets: targets,
            Options:
            [
                AbsentMeansOption(targets, "on", "On"),
                new TweakOption("off", "Off (exclusive fullscreen)", new Dictionary<TweakTarget, MachineValue?>
                {
                    [targets[0]] = RegistryValue.DWord(2),
                    [targets[1]] = RegistryValue.DWord(2),
                    [targets[2]] = RegistryValue.DWord(1),
                    [targets[3]] = RegistryValue.DWord(1),
                }),
            ]);

        static TweakTarget ConfigStore(string name, int? windowsDefault) => new(
            new RegistryLocation(RegistryHive.CurrentUser, @"System\GameConfigStore", name),
            AbsentMeans: windowsDefault is { } value ? RegistryValue.DWord(value) : null);
    }

    private static DeclaredTweak SystemResponsiveness()
    {
        // Microsoft's MMCSS documentation: the share of CPU kept for low-priority work while
        // multimedia tasks run; 20 when missing, and anything under 10 counts as 10. No Recommended
        // Option: only apps that register with MMCSS (audio, some games) are affected.
        var responsiveness = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, MultimediaSystemProfile, "SystemResponsiveness"),
            AbsentMeans: RegistryValue.DWord(20));

        return new DeclaredTweak(
            Id: "gaming.system-responsiveness",
            Title: "CPU kept for background work",
            Description: "While games and audio run under the Multimedia Class Scheduler, Windows keeps a share of the CPU free for everything else. A smaller share leaves more for them.",
            Category: Category.Gaming,
            Group: "Multimedia scheduler",
            Targets: [responsiveness],
            Options: [DWordOption(responsiveness, "10", "10%", 10), DWordOption(responsiveness, "20", "20% (Windows default)", 20)],
            Activation: Activation.AfterRestart);
    }

    private static DeclaredTweak GamesPriority()
    {
        // Microsoft's MMCSS documentation describes the Tasks\Games values; 2 / Medium / Normal are
        // what Windows ships (also used when missing); 6 / High / High stays within the ranges the docs
        // give. No Recommended Option: only games that register with MMCSS as "Games" are affected.
        TweakTarget[] targets =
        [
            GamesValue("Priority", RegistryValue.DWord(2)),
            GamesValue("Scheduling Category", RegistryValue.String("Medium")),
            GamesValue("SFIO Priority", RegistryValue.String("Normal")),
        ];

        return new DeclaredTweak(
            Id: "gaming.games-priority",
            Title: "Priority for games",
            Description: "How strongly the Multimedia Class Scheduler favours games that ask for it, for CPU time and disk access.",
            Category: Category.Gaming,
            Group: "Multimedia scheduler",
            Targets: targets,
            Options:
            [
                new TweakOption("high", "High", new Dictionary<TweakTarget, MachineValue?>
                {
                    [targets[0]] = RegistryValue.DWord(6),
                    [targets[1]] = RegistryValue.String("High"),
                    [targets[2]] = RegistryValue.String("High"),
                }),
                AbsentMeansOption(targets, "windows-default", "Windows default"),
            ],
            Activation: Activation.AfterRestart);

        static TweakTarget GamesValue(string name, RegistryValue windowsDefault) => new(
            new RegistryLocation(RegistryHive.LocalMachine, MultimediaSystemProfile + @"\Tasks\Games", name),
            AbsentMeans: windowsDefault);
    }

    private static DeclaredTweak NetworkThrottling()
    {
        // Microsoft's MMCSS documentation: while multimedia plays, Windows limits other network
        // traffic to this many packets per millisecond; 10 when missing, 0xFFFFFFFF turns the limit
        // off. No Recommended Option: the limit only applies while media is playing.
        var index = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, MultimediaSystemProfile, "NetworkThrottlingIndex"),
            AbsentMeans: RegistryValue.DWord(10));

        return new DeclaredTweak(
            Id: "gaming.network-throttling",
            Title: "Network throttling during media",
            Description: "While music or video plays, Windows slows other network traffic so playback does not stutter. Turning it off keeps game traffic at full speed while you listen to something.",
            Category: Category.Gaming,
            Group: "Multimedia scheduler",
            Targets: [index],
            Options: [DWordOption(index, "off", "Off", unchecked((int)0xFFFFFFFF)), DWordOption(index, "on", "On", 10)],
            Activation: Activation.AfterRestart);
    }

    private static DeclaredTweak AutomaticMaintenance()
    {
        // Akari-OS sets MaintenanceDisabled to 1; AkariOS-Ultimate's revert deletes it. No
        // Recommended Option: maintenance is what runs updates, scans and drive upkeep while idle.
        var disabled = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance", "MaintenanceDisabled"),
            AbsentMeans: RegistryValue.DWord(0));

        return new DeclaredTweak(
            Id: "gaming.automatic-maintenance",
            Title: "Automatic maintenance",
            Description: "Windows runs updates, security scans and disk upkeep when it thinks the PC is idle, which can be while a game is loading or paused. Turned off, these only run when you start them.",
            Category: Category.Gaming,
            Group: "Background activity",
            Targets: [disabled],
            Options: [DWordOption(disabled, "off", "Off", 1), Option(disabled, "on", "On", null)],
            Activation: Activation.AfterRestart);
    }

    private static DeclaredTweak StorageSense()
    {
        // Akari-OS sets the Storage Sense policy (AllowStorageSenseGlobal, documented by Microsoft) to 0,
        // turning it off for every user; deleting the policy hands the choice back to Settings. No
        // Recommended Option: it only runs when space is low or on its schedule.
        var policy = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\StorageSense", "AllowStorageSenseGlobal"));

        return new DeclaredTweak(
            Id: "gaming.storage-sense",
            Title: "Storage Sense",
            Description: "Windows deletes temporary files and empties the Recycle Bin in the background when the drive runs low or on a schedule. Blocking it stops that cleanup from running mid-game; you can still free up space by hand.",
            Category: Category.Gaming,
            Group: "Background activity",
            Targets: [policy],
            Options: [DWordOption(policy, "off", "Off", 0), Option(policy, "per-user", "Each user decides", null)]);
    }

    private static DeclaredTweak StartupDelay()
    {
        // Explorer holds back apps that start at sign-in for a few seconds; StartupDelayInMSec 0
        // removes that wait, and deleting it brings Windows' own delay back. No Recommended Option:
        // it changes how sign-in feels, not how games run. Neither Akari-OS nor Microsoft documents it,
        // so the value is confirmed against Windows itself in the VM.
        var delay = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec"));

        return new DeclaredTweak(
            Id: "gaming.startup-delay",
            Title: "Startup app delay",
            Description: "Windows waits a few seconds after you sign in before starting apps such as launchers and chat. Removing the wait opens them sooner, but they then compete with the desktop while it loads.",
            Category: Category.Gaming,
            Group: "Background activity",
            Targets: [delay],
            Options: [DWordOption(delay, "none", "No delay", 0), Option(delay, "windows-default", "Windows default", null)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak MenuShowDelay()
    {
        // Akari-OS sets "0"; AkariOS-Ultimate restores "400", the Windows default (also used when
        // missing). No Recommended Option: it only changes how quickly menus open.
        var delay = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Control Panel\Desktop", "MenuShowDelay"),
            AbsentMeans: RegistryValue.String("400"));

        return new DeclaredTweak(
            Id: "gaming.menu-show-delay",
            Title: "Submenu delay",
            Description: "How long Windows waits before opening a submenu when you point at it.",
            Category: Category.Gaming,
            Group: "Visual effects",
            Targets: [delay],
            Options:
            [
                Option(delay, "instant", "Instant", RegistryValue.String("0")),
                Option(delay, "short", "Short (200 ms)", RegistryValue.String("200")),
                Option(delay, "windows-default", "Windows default (400 ms)", RegistryValue.String("400")),
            ],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak WindowAnimations()
    {
        // Akari-OS sets MinAnimate to "0"; "1" is the Windows default, also used when missing.
        var animate = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate"),
            AbsentMeans: RegistryValue.String("1"));

        return new DeclaredTweak(
            Id: "gaming.window-animations",
            Title: "Minimize and maximize animations",
            Description: "Windows animates windows as they minimize, maximize and restore. Turned off, they snap into place at once.",
            Category: Category.Gaming,
            Group: "Visual effects",
            Targets: [animate],
            Options: [Option(animate, "off", "Off", RegistryValue.String("0")), Option(animate, "on", "On", RegistryValue.String("1"))],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak TaskbarAnimations()
    {
        // Akari-OS sets TaskbarAnimations to 0; 1 is the Windows default, also used when missing.
        var animations = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations"),
            AbsentMeans: RegistryValue.DWord(1));

        return new DeclaredTweak(
            Id: "gaming.taskbar-animations",
            Title: "Taskbar animations",
            Description: "Animations on the taskbar, such as icons sliding into place and thumbnails fading in.",
            Category: Category.Gaming,
            Group: "Visual effects",
            Targets: [animations],
            Options: [DWordOption(animations, "off", "Off", 0), DWordOption(animations, "on", "On", 1)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak Peek()
    {
        // Akari-OS sets EnableAeroPeek to 0; 1 is the Windows default, also used when missing.
        var peek = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\DWM", "EnableAeroPeek"),
            AbsentMeans: RegistryValue.DWord(1));

        return new DeclaredTweak(
            Id: "gaming.peek",
            Title: "Peek at the desktop",
            Description: "Pointing at the far right of the taskbar, or at a window's thumbnail, makes the other windows see-through so you can look behind them.",
            Category: Category.Gaming,
            Group: "Visual effects",
            Targets: [peek],
            Options: [DWordOption(peek, "off", "Off", 0), DWordOption(peek, "on", "On", 1)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak Transparency()
    {
        // Akari-OS sets EnableTransparency to 0; 1 is the Windows default, also used when missing.
        var transparency = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency"),
            AbsentMeans: RegistryValue.DWord(1));

        return new DeclaredTweak(
            Id: "gaming.transparency",
            Title: "Transparency effects",
            Description: "The blurred, see-through backgrounds of the taskbar, Start and Settings. Turned off, they become solid colours and the graphics card does a little less work.",
            Category: Category.Gaming,
            Group: "Visual effects",
            Targets: [transparency],
            Options: [DWordOption(transparency, "off", "Off", 0), DWordOption(transparency, "on", "On", 1)]);
    }

    private static DeclaredTweak DragFullWindows()
    {
        // Akari-OS sets DragFullWindows to "0"; "1" is the Windows default, also used when missing.
        var drag = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Control Panel\Desktop", "DragFullWindows"),
            AbsentMeans: RegistryValue.String("1"));

        return new DeclaredTweak(
            Id: "gaming.drag-full-windows",
            Title: "Show window contents while dragging",
            Description: "Windows redraws a window's contents all the way as you drag it. Turned off, only an outline moves until you let go.",
            Category: Category.Gaming,
            Group: "Visual effects",
            Targets: [drag],
            Options: [Option(drag, "off", "Off", RegistryValue.String("0")), Option(drag, "on", "On", RegistryValue.String("1"))],
            Activation: Activation.AfterSignOut);
    }

    /// <summary>An On/Off Tweak over one current-user DWORD, with Windows' value when it is missing.</summary>
    private static DeclaredTweak GamingOnOff(
        string id, string title, string description, string group, string key, string name,
        int onValue, int offValue, int absent, Activation activation = Activation.Immediately)
    {
        var target = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, key, name), AbsentMeans: RegistryValue.DWord(absent));

        return new DeclaredTweak(id, title, description, Category.Gaming, group, [target],
            [DWordOption(target, "off", "Off", offValue), DWordOption(target, "on", "On", onValue)],
            Activation: activation);
    }

    private static DeclaredTweak GameBarTips() =>
        // Winhance turns ShowStartupPanel off. Windows shows the panel when the value is missing.
        GamingOnOff("gaming.game-bar-tips", "Game Bar tips on startup",
            "Shows a panel of tips the first times you open Game Bar, over the top of your game.",
            "Game Bar", @"Software\Microsoft\GameBar", "ShowStartupPanel", onValue: 1, offValue: 0, absent: 1);

    private static DeclaredTweak NvidiaSharpening()
    {
        // Winhance: EnableGR535 = 0 turns the legacy image sharpening on, 1 (also when missing) off.
        // Only older NVIDIA drivers read it; newer ones have Image Scaling in the NVIDIA app instead.
        var gr535 = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SOFTWARE\NVIDIA Corporation\Global\FTS", "EnableGR535"),
            AbsentMeans: RegistryValue.DWord(1));

        return new DeclaredTweak(
            Id: "gaming.nvidia-sharpening",
            Title: "Legacy NVIDIA image sharpening",
            Description: "Turns on the image sharpening filter of older NVIDIA drivers. Current drivers ignore it and use Image Scaling in the NVIDIA app instead.",
            Category: Category.Gaming,
            Group: "Graphics",
            Targets: [gr535],
            Options: [DWordOption(gr535, "off", "Off", 1), DWordOption(gr535, "on", "On", 0)],
            RequiresGpu: GpuVendor.Nvidia,
            Activation: Activation.AfterRestart);
    }

    private static DeclaredTweak GamesGpuPriority()
    {
        // Winhance switches GPU Priority between 8 and 2. 8 is already Windows' value (read on build
        // 26300), so the choice here is whether to lower it.
        var priority = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, MultimediaSystemProfile + @"\Tasks\Games", "GPU Priority"),
            AbsentMeans: RegistryValue.DWord(8));

        return new DeclaredTweak(
            Id: "gaming.games-gpu-priority",
            Title: "GPU priority for games",
            Description: "How strongly the Multimedia Class Scheduler favours games that ask for it on the graphics card. Windows already sets it high; Low gives other apps more of the GPU.",
            Category: Category.Gaming,
            Group: "Multimedia scheduler",
            Targets: [priority],
            Options: [DWordOption(priority, "high", "High (Windows default)", 8), DWordOption(priority, "low", "Low", 2)],
            Activation: Activation.AfterRestart);
    }

    private static DeclaredTweak Prefetch()
    {
        // Winhance switches EnablePrefetcher between 3 (apps and startup; Windows' value) and 0.
        var prefetcher = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnablePrefetcher"),
            AbsentMeans: RegistryValue.DWord(3));

        return new DeclaredTweak(
            Id: "gaming.prefetch",
            Title: "Prefetch",
            Description: "Windows records which files apps and startup read, and loads them ahead of time next time. It helps most on hard drives; on an SSD the gain is small.",
            Category: Category.Gaming,
            Group: "Background activity",
            Targets: [prefetcher],
            Options: [DWordOption(prefetcher, "off", "Off", 0), DWordOption(prefetcher, "on", "On", 3)],
            Activation: Activation.AfterRestart);
    }

    private static DeclaredTweak ThumbnailsInsteadOfIcons() =>
        // Akari-OS and Winhance: IconsOnly 0 shows thumbnails, which is also Windows' value when missing.
        GamingOnOff("gaming.thumbnails", "Thumbnails instead of icons",
            "File Explorer shows a small preview of pictures and videos. Turned off, it shows only file icons, which is quicker in big folders.",
            "Visual effects", ExplorerAdvanced, "IconsOnly", onValue: 0, offValue: 1, absent: 0, Activation.AfterSignOut);

    private static DeclaredTweak TranslucentSelection() =>
        // Akari-OS turns ListviewAlphaSelect off; Windows treats a missing value as on.
        GamingOnOff("gaming.translucent-selection", "Translucent selection rectangle",
            "Dragging to select files draws a see-through blue box. Turned off, it draws a plain dotted outline.",
            "Visual effects", ExplorerAdvanced, "ListviewAlphaSelect", onValue: 1, offValue: 0, absent: 1, Activation.AfterSignOut);

    private static DeclaredTweak IconLabelShadows() =>
        // Akari-OS turns ListviewShadow off; Windows treats a missing value as on.
        GamingOnOff("gaming.icon-label-shadows", "Shadows under desktop icon labels",
            "Draws a shadow under the names of desktop icons so they stay readable on any wallpaper.",
            "Visual effects", ExplorerAdvanced, "ListviewShadow", onValue: 1, offValue: 0, absent: 1, Activation.AfterSignOut);

    private static DeclaredTweak SaveTaskbarThumbnails() =>
        // Akari-OS keeps AlwaysHibernateThumbnails off (0, also Windows' value when missing).
        GamingOnOff("gaming.save-taskbar-thumbnails", "Save taskbar thumbnail previews",
            "Keeps the previews you see when pointing at a taskbar button in memory, even for minimized windows.",
            "Visual effects", @"Software\Microsoft\Windows\DWM", "AlwaysHibernateThumbnails", onValue: 1, offValue: 0, absent: 0, Activation.AfterSignOut);

    private static DeclaredTweak FontSmoothing()
    {
        // Akari-OS and Winhance: FontSmoothing "2" smooths font edges (Windows' value), "0" turns it off.
        var smoothing = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Control Panel\Desktop", "FontSmoothing"),
            AbsentMeans: RegistryValue.String("2"));

        return new DeclaredTweak(
            Id: "gaming.font-smoothing",
            Title: "Smooth edges of screen fonts",
            Description: "Softens the jagged edges of text on screen. Turned off, text looks rougher; almost everyone wants this on.",
            Category: Category.Gaming,
            Group: "Visual effects",
            Targets: [smoothing],
            Options: [Option(smoothing, "off", "Off", RegistryValue.String("0")), Option(smoothing, "on", "On", RegistryValue.String("2"))],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak WallpaperQuality()
    {
        // JPEGImportQuality (documented by ElevenForum and NinjaOne): Windows re-saves a JPG wallpaper
        // at 85% quality when the value is missing; 100 keeps it as it is.
        var quality = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Control Panel\Desktop", "JPEGImportQuality"),
            AbsentMeans: RegistryValue.DWord(85));

        return new DeclaredTweak(
            Id: "gaming.wallpaper-quality",
            Title: "Desktop wallpaper quality",
            Description: "Windows shrinks JPG wallpapers to 85% quality, which can blur them. Full quality keeps them sharp. Set your wallpaper again afterwards for it to apply.",
            Category: Category.Gaming,
            Group: "Visual effects",
            Targets: [quality],
            Options: [DWordOption(quality, "full", "Full quality", 100), Option(quality, "compressed", "Compressed (Windows default)", null)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak SvcHostSplit()
    {
        // AkariOS-Ultimate's SvcHost split tuner, which sits next to its processor scheduling tuner and
        // is drawn the same way here. Windows runs services in their own svchost.exe when RAM is above
        // SvcHostSplitThresholdInKB (3670016 KB, about 3.5 GB, by default). Match RAM sets it to this
        // PC's RAM rounded up to whole GB, as AkariOS-Ultimate does; 0xFFFFFFFF never splits.
        var threshold = new TweakTarget(
            new RegistryLocation(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control", "SvcHostSplitThresholdInKB"),
            AbsentMeans: RegistryValue.DWord(3670016));

        var windowsDefault = DWordOption(threshold, "windows-default", "Windows default", 3670016);
        List<TweakOption> options = [windowsDefault];
        string? ram = null;
        if (MatchRamKilobytes() is { } matchRam)
        {
            options.Add(DWordOption(threshold, "match-ram", "Match RAM", unchecked((int)matchRam)));
            ram = $"RAM: {matchRam / (1024 * 1024)} GB";
        }

        options.Add(DWordOption(threshold, "never", "Never split", unchecked((int)0xFFFFFFFF)));

        return new DeclaredTweak(
            Id: "gaming.svchost-split",
            Title: "SvcHost split threshold",
            Description: "Controls how many svchost.exe processes Windows creates. Merging them saves memory, but one crash takes more services down. Restart to apply.",
            Category: Category.Gaming,
            Group: "Processor",
            Targets: [threshold],
            Options: options,
            Activation: Activation.AfterRestart,
            Tuner: new TunerLayout(windowsDefault, "KB", ram));

        // This PC's RAM rounded up to whole GB, in KB, kept below 0xFFFFFFFF (Never split).
        static uint? MatchRamKilobytes()
        {
            if (WindowsMachine.InstalledRamKilobytes() is not { } kilobytes || kilobytes == 0)
            {
                return null;
            }

            const ulong gigabyte = 1024 * 1024;
            var rounded = (kilobytes + gigabyte - 1) / gigabyte * gigabyte;
            return rounded >= uint.MaxValue ? null : (uint)rounded;
        }
    }

    private static DeclaredTweak SearchWebView2()
    {
        // A Windows feature override (NTLite community; the same as vivetool /disable /id:37926450):
        // EnabledState 1 turns the WebView2-based search box off on builds 26200.7462 and later.
        // NTLite's version also writes four zero values, but EnabledState alone does it (seen on build
        // 26300), and that is what vivetool and AkariOS-Ultimate leave behind. Deleting it hands the
        // feature back to Windows. It is not a documented setting, so an update may reset it (Drift).
        var state = new TweakTarget(new RegistryLocation(
            RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260", "EnabledState"));

        return new DeclaredTweak(
            Id: "gaming.search-webview2",
            Title: "WebView2 in Windows Search",
            Description: "The search box in Start and the taskbar runs on WebView2, an Edge engine that keeps several background processes running. Off switches to the older search box without web content, on Windows 11 build 26200.7462 and later.",
            Category: Category.Gaming,
            Group: "Background activity",
            Targets: [state],
            Options: [Option(state, "off", "Off", RegistryValue.DWord(1)), Option(state, "windows-decides", "Let Windows decide", null)],
            Activation: Activation.AfterRestart);
    }

    private static DeclaredTweak AccessibilityShortcut(string id, string title, string description, string key, string onFlags, string offFlags)
    {
        // Akari-OS and Winhance write these Flags strings, as for Sticky Keys; the on value is Windows' own.
        var flags = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, $@"Control Panel\Accessibility\{key}", "Flags"),
            AbsentMeans: RegistryValue.String(onFlags));

        return new DeclaredTweak(id, title, description, Category.Gaming, "Accessibility", [flags],
            [Option(flags, "off", "Off", RegistryValue.String(offFlags)), Option(flags, "on", "On", RegistryValue.String(onFlags))],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak NarratorShortcut() =>
        // Winhance turns WinEnterLaunchEnabled off; Windows treats a missing value as on.
        GamingOnOff("gaming.narrator-shortcut", "Narrator shortcut",
            "Windows + Ctrl + Enter starts Narrator, the screen reader, which then reads everything aloud.",
            "Accessibility", @"Software\Microsoft\Narrator\NoRoam", "WinEnterLaunchEnabled", onValue: 1, offValue: 0, absent: 1);

    private const string MultimediaSystemProfile = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
}
