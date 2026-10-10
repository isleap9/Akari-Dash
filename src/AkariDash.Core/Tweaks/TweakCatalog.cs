using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

/// <summary>Every Tweak Akari-Dash ships.</summary>
public static class TweakCatalog
{
    public static IReadOnlyList<DeclaredTweak> All { get; } =
    [
        GameMode(),
        ProcessorScheduling(),
        SysMain(),
        ScheduledDriveOptimization(),
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

    private static DeclaredTweak SysMain()
    {
        // Akari-OS disables SysMain (Automatic is the Windows default).
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
            Recommended: off);
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

    private static TweakOption Option(TweakTarget target, string id, string label, MachineValue value) =>
        new(id, label, new Dictionary<TweakTarget, MachineValue?> { [target] = value });

    private static TweakOption DWordOption(TweakTarget target, string id, string label, int value) =>
        Option(target, id, label, RegistryValue.DWord(value));
}
