using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// The combined preview for "apply all recommended": the Tweaks that would be put into their
/// Recommended Option, with what each would change, and the Tweaks left alone with why.
/// </summary>
public sealed record RecommendedPlan(IReadOnlyList<PlannedTweak> ToApply, IReadOnlyList<SkippedTweak> Skipped);

/// <summary>Applying <paramref name="Option"/> to <paramref name="Tweak"/> would make <paramref name="Changes"/>.</summary>
public sealed record PlannedTweak(DeclaredTweak Tweak, TweakOption Option, IReadOnlyList<PlannedChange> Changes);

/// <summary>A Tweak "apply all recommended" leaves alone; <paramref name="Reason"/> says why, as shown to the user.</summary>
public sealed record SkippedTweak(DeclaredTweak Tweak, string Reason);

/// <summary>
/// How applying one Tweak went. <paramref name="Error"/> is <see langword="null"/> on success. On failure
/// it is usually a <see cref="TweakApplyException"/>, which says whether the Tweak was rolled back.
/// </summary>
public sealed record TweakResult(DeclaredTweak Tweak, TweakOption Option, Exception? Error)
{
    public bool Succeeded => Error is null;
}
