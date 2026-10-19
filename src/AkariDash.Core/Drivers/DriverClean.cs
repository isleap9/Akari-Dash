using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>
/// The safe-mode half of a DDU clean. The Drivers page prepares DDU with AkariOS-Ultimate's own script
/// (driver-clean-prepare.ps1) and queues one of these passes; the companion that does Disable
/// Defender's safe-mode writes runs DDU from it, so a clean shares that queue, RunOnce entry and
/// restart instead of AkariOS's RunOnce .ps1.
/// </summary>
public static class DriverClean
{
    /// <summary>The id a DDU pass is queued under; no Tweak has it.</summary>
    public const string TweakId = "driver-clean";

    /// <summary>DDU cleans by itself (AkariOS's DDU: Auto).</summary>
    public const string Auto = "auto";

    /// <summary>DDU is opened for the person to clean by hand (AkariOS's DDU: Manual).</summary>
    public const string Manual = "manual";

    /// <summary>Where the prepare script extracts DDU.</summary>
    public static string Executable { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp", "ddu", "Display Driver Uninstaller.exe");

    public static SafeModePass Pass(string mode) => new(TweakId, mode, Undo: false);

    public static bool IsFor(SafeModePass pass) => pass.TweakId == TweakId;

    /// <summary>
    /// DDU's command line. Auto is AkariOS's, less <c>-Restart</c>: the companion restarts once the
    /// outcome is saved. Manual opens DDU with nothing, as AkariOS does.
    /// </summary>
    public static IReadOnlyList<string> Arguments(string mode) =>
        mode == Auto ? ["-CleanSoundBlaster", "-CleanRealtek", "-CleanAllGpus"] : [];
}
