namespace AkariDash.App;

/// <summary>Facts about how this build was configured.</summary>
public static class BuildInfo
{
    /// <summary>
    /// True in the Phase 1 build configuration, where every apply is a Dry Run and the
    /// shell shows a persistent "Dry Run only" banner.
    /// </summary>
#if PHASE1
    public static bool IsDryRunOnly => true;
#else
    public static bool IsDryRunOnly => false;
#endif
}
