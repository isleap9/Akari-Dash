namespace AkariDash.Core.Tweaks;

/// <summary>
/// A Tweak Akari-Dash applied that is no longer in the catalog. Only its id and Original Values are
/// known, which is enough to Undo it.
/// </summary>
public sealed record RetiredTweak(string Id, IReadOnlyList<OriginalValue> OriginalValues);
