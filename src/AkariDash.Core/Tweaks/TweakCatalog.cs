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
    ];

    private static DeclaredTweak GameMode()
    {
        // Windows treats a missing value as Game Mode on.
        var enabled = new RegistryTarget(
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
        var separation = new RegistryTarget(
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

    private static TweakOption DWordOption(RegistryTarget target, string id, string label, int value) =>
        new(id, label, new Dictionary<RegistryTarget, RegistryValue?> { [target] = RegistryValue.DWord(value) });
}
