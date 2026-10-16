using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// Something a <see cref="DeclaredTweak"/> touches: a registry value, a service's start type or a
/// scheduled task's enabled state.
/// <paramref name="AbsentMeans"/> is what Windows behaves as when it does not exist
/// (e.g. Game Mode is on by default), so a missing value can still resolve to an Option.
/// <paramref name="WrittenInOwnSession"/> says Akari-Dash writes this one itself, in the session that
/// asked for the Tweak, rather than leaving it to the safe-mode pass.
/// </summary>
public sealed record TweakTarget(MachineLocation Location, MachineValue? AbsentMeans = null, bool WrittenInOwnSession = false)
{
    /// <summary>
    /// What Windows behaves as when this target holds <paramref name="value"/>: the value itself,
    /// or <see cref="AbsentMeans"/> when it does not exist. Two values compare as the same state
    /// only through this, so a missing value and the value it stands for are not told apart.
    /// </summary>
    public MachineValue? Effective(MachineValue? value) => value ?? AbsentMeans;
}
