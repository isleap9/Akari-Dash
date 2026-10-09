using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

/// <summary>Every Tweak Akari-Dash ships.</summary>
public static class TweakCatalog
{
    public static IReadOnlyList<DeclaredTweak> All { get; } =
    [
        GameMode(),
    ];

    private static DeclaredTweak GameMode()
    {
        // Windows treats a missing value as Game Mode on.
        var enabled = new RegistryTarget(
            new RegistryLocation(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled"),
            AbsentMeans: RegistryValue.DWord(1));

        return new DeclaredTweak(
            Id: "gaming.game-mode",
            Title: "Game Mode",
            Description: "Lets Windows prioritise the game you are playing, for example by holding back Windows Update driver installs and restart notifications while it runs.",
            Category: Category.Gaming,
            Group: "Game Mode",
            Targets: [enabled],
            Options:
            [
                new TweakOption("off", "Off", new Dictionary<RegistryTarget, RegistryValue?> { [enabled] = RegistryValue.DWord(0) }),
                new TweakOption("on", "On", new Dictionary<RegistryTarget, RegistryValue?> { [enabled] = RegistryValue.DWord(1) }),
            ]);
    }
}
