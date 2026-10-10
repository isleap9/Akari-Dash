using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// What Akari-Dash remembers about a Tweak it has applied: the machine's own values from just
/// before the first apply, and the Option it last applied.
/// </summary>
public sealed record AppliedTweak(string LastAppliedOptionId, IReadOnlyList<OriginalValue> OriginalValues);

/// <summary>One target's Original Value; <see langword="null"/> means it did not exist.</summary>
public sealed record OriginalValue(MachineLocation Location, MachineValue? Value);
