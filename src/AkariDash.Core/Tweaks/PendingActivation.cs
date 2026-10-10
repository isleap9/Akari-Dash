namespace AkariDash.Core.Tweaks;

/// <summary>
/// The Tweaks applied or undone this session whose change is still waiting on a sign-out or a
/// restart, each listed once in the order it was first changed.
/// </summary>
public sealed record PendingActivation(IReadOnlyList<DeclaredTweak> AfterSignOut, IReadOnlyList<DeclaredTweak> AfterRestart)
{
    public bool IsEmpty => AfterSignOut.Count == 0 && AfterRestart.Count == 0;

    /// <summary>A restart also signs out, so when anything waits on a restart, one restart finishes everything.</summary>
    public bool NeedsRestart => AfterRestart.Count > 0;
}
