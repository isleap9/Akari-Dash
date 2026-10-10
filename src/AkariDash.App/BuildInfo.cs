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

    /// <summary>Shown while the session's first real apply waits for its restore point.</summary>
    public const string RestorePointNote = "Creating a restore point first; this can take a minute.";

    /// <summary>Ends an apply preview: in the Phase 1 build, says that nothing will be written.</summary>
    public static string PreviewNote => IsDryRunOnly
        ? Environment.NewLine + Environment.NewLine + "Dry Run only: nothing will be written to this PC."
        : string.Empty;
}
