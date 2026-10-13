using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// Every Tweak Akari-Dash ships, one file per Category: TweakCatalog.Gaming.cs,
/// TweakCatalog.Network.cs and TweakCatalog.ScheduledTasks.cs (both on the Gaming page),
/// TweakCatalog.Privacy.cs, TweakCatalog.Services.cs, TweakCatalog.Security.cs,
/// TweakCatalog.Windows.cs, TweakCatalog.Notifications.cs, TweakCatalog.Update.cs, and
/// TweakCatalog.StartMenu.cs with TweakCatalog.Taskbar.cs (two of the three Customize pages).
/// This file holds the list and the helpers they share.
/// </summary>
public static partial class TweakCatalog
{
    public static IReadOnlyList<DeclaredTweak> All { get; } =
    [
        .. GamingTweaks(),
        .. ScheduledTaskTweaks(),
        .. AccessibilityTweaks(),
        .. PrivacyTweaks(),
        .. ServicesTweaks(),
        .. SecurityTweaks(),
        .. WindowsTweaks(),
        .. NotificationTweaks(),
        .. UpdateTweaks(),
        .. StartMenuTweaks(),
        .. TaskbarTweaks(),
    ];

    private static TweakOption Option(TweakTarget target, string id, string label, MachineValue? value) =>
        new(id, label, new Dictionary<TweakTarget, MachineValue?> { [target] = value });

    private static TweakOption DWordOption(TweakTarget target, string id, string label, int value) =>
        Option(target, id, label, RegistryValue.DWord(value));

    private static TweakOption Option(IEnumerable<TweakTarget> targets, string id, string label, MachineValue? value) =>
        new(id, label, targets.ToDictionary(target => target, _ => value));

    private static TweakOption DWordOption(IEnumerable<TweakTarget> targets, string id, string label, int value) =>
        Option(targets, id, label, RegistryValue.DWord(value));

    /// <summary>The Option where every target holds what Windows behaves as when it is missing.</summary>
    private static TweakOption AbsentMeansOption(IEnumerable<TweakTarget> targets, string id, string label) =>
        new(id, label, targets.ToDictionary(target => target, target => target.AbsentMeans));
}
