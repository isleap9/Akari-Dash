namespace AkariDash.Core.Tweaks;

/// <summary>A Tweak described entirely as data and executed by the shared <see cref="TweakEngine"/>.</summary>
public sealed record DeclaredTweak(
    string Id,
    string Title,
    string Description,
    Category Category,
    string Group,
    IReadOnlyList<RegistryTarget> Targets,
    IReadOnlyList<TweakOption> Options);
