namespace AkariDash.Core.Tweaks;

/// <summary>
/// A Tweak described entirely as data and executed by the shared <see cref="TweakEngine"/>.
/// <paramref name="Recommended"/> is one of <paramref name="Options"/>, or <see langword="null"/>
/// for a Tweak that is a matter of taste.
/// </summary>
public sealed record DeclaredTweak(
    string Id,
    string Title,
    string Description,
    Category Category,
    string Group,
    IReadOnlyList<TweakTarget> Targets,
    IReadOnlyList<TweakOption> Options,
    TweakOption? Recommended = null);
