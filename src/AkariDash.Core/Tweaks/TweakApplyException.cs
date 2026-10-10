using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// A Tweak could not be applied or undone because writing <see cref="FailedTarget"/> failed.
/// When <see cref="RolledBack"/> is true every target already written was put back, so the
/// machine is as it was before the attempt; otherwise <see cref="NotRolledBack"/> lists the
/// targets that could not be put back.
/// </summary>
public sealed class TweakApplyException(
    DeclaredTweak tweak,
    MachineLocation failedTarget,
    IReadOnlyList<MachineLocation> notRolledBack,
    Exception inner)
    : Exception(Describe(tweak, failedTarget, notRolledBack, inner), inner)
{
    public DeclaredTweak Tweak { get; } = tweak;

    public MachineLocation FailedTarget { get; } = failedTarget;

    public IReadOnlyList<MachineLocation> NotRolledBack { get; } = notRolledBack;

    public bool RolledBack => NotRolledBack.Count == 0;

    private static string Describe(
        DeclaredTweak tweak, MachineLocation failedTarget, IReadOnlyList<MachineLocation> notRolledBack, Exception inner) =>
        $"{tweak.Title}: could not change {failedTarget} ({inner.Message}). " +
        (notRolledBack.Count == 0
            ? "Nothing was changed."
            : $"These could not be put back and may be left changed: {string.Join(", ", notRolledBack)}. Use Undo to try again.");
}
