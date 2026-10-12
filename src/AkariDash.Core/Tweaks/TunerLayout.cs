namespace AkariDash.Core.Tweaks;

/// <summary>
/// Draws a Tweak as a tuner card, as AkariOS-Ultimate does: its Options as chips, a line with the
/// current value in <paramref name="Unit"/> and an optional <paramref name="Detail"/>, and Read
/// current, Default (which applies <paramref name="DefaultOption"/>) and Apply buttons. It changes
/// only how the Tweak looks; applying and Undo work as for any other Tweak.
/// </summary>
public sealed record TunerLayout(TweakOption DefaultOption, string Unit, string? Detail = null);
