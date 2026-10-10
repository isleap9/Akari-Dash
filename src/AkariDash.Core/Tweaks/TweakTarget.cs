using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// Something a <see cref="DeclaredTweak"/> touches: a registry value, a service's start type or a
/// scheduled task's enabled state.
/// <paramref name="AbsentMeans"/> is what Windows behaves as when it does not exist
/// (e.g. Game Mode is on by default), so a missing value can still resolve to an Option.
/// </summary>
public sealed record TweakTarget(MachineLocation Location, MachineValue? AbsentMeans = null);
