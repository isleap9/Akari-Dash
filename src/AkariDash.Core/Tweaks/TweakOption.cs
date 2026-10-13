using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// One named state a Tweak can be put in: the value of every target in that state.
/// A <see langword="null"/> value means the target does not exist.
/// </summary>
/// <param name="Activation">
/// When this Option in particular takes effect, for the rare Tweak where one Option waits longer
/// than the rest: switching UAC off outright needs a restart while every other level is immediate.
/// <see langword="null"/> means the Tweak's own Activation, which is the usual case.
/// </param>
public sealed record TweakOption(
    string Id,
    string Label,
    IReadOnlyDictionary<TweakTarget, MachineValue?> Values,
    Activation? Activation = null);
