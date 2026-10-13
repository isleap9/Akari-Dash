using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// A Tweak described entirely as data and executed by the shared <see cref="TweakEngine"/>.
/// <paramref name="Recommended"/> is one of <paramref name="Options"/>, or <see langword="null"/>
/// for a Tweak that is a matter of taste. A Tweak with <paramref name="RequiresGpu"/> is Unavailable
/// on machines without a graphics card from that vendor. <paramref name="Activation"/> says when
/// applying or undoing it takes effect. A Tweak with <paramref name="Tuner"/> is drawn as a tuner card
/// instead of a row.
/// </summary>
public sealed record DeclaredTweak(
    string Id,
    string Title,
    string Description,
    Category Category,
    string Group,
    IReadOnlyList<TweakTarget> Targets,
    IReadOnlyList<TweakOption> Options,
    TweakOption? Recommended = null,
    GpuVendor? RequiresGpu = null,
    Activation Activation = Activation.Immediately,
    TunerLayout? Tuner = null)
{
    /// <summary>
    /// When putting this Tweak into <paramref name="option"/> takes effect: the Option's own
    /// Activation where it has one, otherwise the Tweak's.
    /// </summary>
    public Activation ActivationOf(TweakOption option) => option.Activation ?? Activation;
}
