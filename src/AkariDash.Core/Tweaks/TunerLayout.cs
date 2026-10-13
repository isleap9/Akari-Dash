namespace AkariDash.Core.Tweaks;

/// <summary>
/// Draws a Tweak as a tuner card, as AkariOS-Ultimate does: its Options as chips, a line with the
/// current value in <paramref name="Unit"/> and an optional <paramref name="Detail"/>, and Read
/// current, Default (which applies <paramref name="DefaultOption"/>) and Apply buttons. It changes
/// only how the Tweak looks; applying and Undo work as for any other Tweak.
/// </summary>
/// <param name="Chips">
/// Which Options to draw as chips, under the names the card calls them by, for a Tweak with more
/// Options than belong on a card: processor scheduling offers all 27 combinations, of which three
/// are worth a chip. The rest stay reachable from the card's own list. <see langword="null"/> draws
/// every Option as a chip, labelled as the Option is.
/// </param>
/// <param name="Hex">
/// Shows the current value as hex with its decimal in brackets, as Win32PrioritySeparation is
/// usually written, instead of a number with <paramref name="Unit"/>.
/// </param>
public sealed record TunerLayout(
    TweakOption DefaultOption,
    string Unit,
    string? Detail = null,
    IReadOnlyList<TunerChip>? Chips = null,
    bool Hex = false);

/// <summary>One chip on a tuner card: an Option under the name the card calls it by.</summary>
public sealed record TunerChip(TweakOption Option, string Label);
