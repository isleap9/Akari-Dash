using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// A registry value a <see cref="DeclaredTweak"/> touches.
/// <paramref name="AbsentMeans"/> is what Windows behaves as when the value does not exist
/// (e.g. Game Mode is on by default), so a missing value can still resolve to an Option.
/// </summary>
public sealed record RegistryTarget(RegistryLocation Location, RegistryValue? AbsentMeans = null);
