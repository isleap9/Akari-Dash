using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

/// <summary>Every Tweak Akari-Dash ships.</summary>
public static class TweakCatalog
{
    public static IReadOnlyList<DeclaredTweak> All { get; } =
    [
        GameMode(),
        GameCaptures(),
        BackgroundRecording(),
        GameBarControllerButton(),
        HardwareGpuScheduling(),
        ProcessorScheduling(),
        PowerThrottling(),
        SysMain(),
        ScheduledDriveOptimization(),
        BackgroundApps(),
        MouseAcceleration(),
        StickyKeysShortcut(),
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

    private static DeclaredTweak SysMain()
    {
        // Akari-OS disables SysMain (Automatic is the Windows default). Only the start type
        // changes, so a running SysMain keeps running until the next restart.
        var service = new TweakTarget(new ServiceLocation("SysMain"));
        var off = Option(service, "off", "Off", new ServiceStartValue(ServiceStartType.Disabled));

        return new DeclaredTweak(
            Id: "gaming.sysmain",
            Title: "SysMain (Superfetch)",
            Description: "Preloads the apps you use most into memory so they open faster. On an SSD the gain is small, and its background disk and memory work can cause stutter while you play.",
            Category: Category.Gaming,
            Group: "Background activity",
            Targets: [service],
            Options: [off, Option(service, "on", "On", new ServiceStartValue(ServiceStartType.Automatic))],
            Recommended: off,
            Activation: Activation.AfterRestart);
    }

    private static DeclaredTweak ScheduledDriveOptimization()
    {
        // Akari-OS disables this task. No Recommended Option: on an SSD it is what sends TRIM, so
        // turning it off trades background disk activity for drive upkeep.
        var task = new TweakTarget(new ScheduledTaskLocation(@"\Microsoft\Windows\Defrag\ScheduledDefrag"));

        return new DeclaredTweak(
            Id: "gaming.scheduled-drive-optimization",
            Title: "Scheduled drive optimization",
            Description: "Windows defragments hard drives and trims SSDs on a weekly schedule in the background. Turning it off stops it starting mid-game; you can still optimize drives by hand.",
            Category: Category.Gaming,
            Group: "Background activity",
            Targets: [task],
            Options: [Option(task, "off", "Off", TaskEnabledValue.Off), Option(task, "on", "On", TaskEnabledValue.On)]);
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
                new TweakOption("on", "On", targets.ToDictionary(target => target, target => target.AbsentMeans)),
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
            Group: "Input",
            Targets: [flags],
            Options: [Option(flags, "off", "Off", RegistryValue.String("2")), Option(flags, "on", "On", RegistryValue.String("510"))],
            Activation: Activation.AfterSignOut);
    }

    private static TweakOption Option(TweakTarget target, string id, string label, MachineValue? value) =>
        new(id, label, new Dictionary<TweakTarget, MachineValue?> { [target] = value });

    private static TweakOption DWordOption(TweakTarget target, string id, string label, int value) =>
        Option(target, id, label, RegistryValue.DWord(value));

    private static TweakOption Option(IEnumerable<TweakTarget> targets, string id, string label, MachineValue? value) =>
        new(id, label, targets.ToDictionary(target => target, _ => value));

    private static TweakOption DWordOption(IEnumerable<TweakTarget> targets, string id, string label, int value) =>
        Option(targets, id, label, RegistryValue.DWord(value));
}
