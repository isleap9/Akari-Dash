namespace AkariDash.Core.Tweaks;

/// <summary>
/// A Tweak was previewed or applied while Unavailable on this machine; nothing was previewed,
/// written or saved. <see cref="Reason"/> says why, as shown to the user.
/// </summary>
public sealed class TweakUnavailableException(DeclaredTweak tweak, string reason)
    : Exception($"{tweak.Title} is unavailable: {reason}")
{
    public DeclaredTweak Tweak { get; } = tweak;

    public string Reason { get; } = reason;
}
