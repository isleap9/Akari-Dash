namespace AkariDash.Core.Apps;

/// <summary>What winget's exit code means for an install.</summary>
public static class WingetInstallResult
{
    /// <summary>
    /// How the install ended, or <see langword="null"/> when the exit code is a real failure and the
    /// caller should throw with winget's own last words. Codes come from winget's published table
    /// (the one "winget error --output" writes): 0x8A150109 and 0x8A15010B are its "restart to
    /// finish" pair, which the runtimes in the catalogue return, and 0x8A150061 and 0x8A15010D both
    /// mean a version of it is already on the machine. 0x8A15010A deliberately is not here: that one
    /// (REBOOT_REQUIRED_FOR_INSTALL) installed nothing. 3010 and 1641 are the raw Windows installer
    /// codes winget turns into its own, kept for anything that surfaces them unchanged.
    /// </summary>
    public static InstallOutcome? Interpret(int exitCode) => exitCode switch
    {
        0 => InstallOutcome.Installed,
        3010 or 1641 => InstallOutcome.InstalledNeedsRestart,
        unchecked((int)0x8A150109) or unchecked((int)0x8A15010B) => InstallOutcome.InstalledNeedsRestart,
        unchecked((int)0x8A150061) or unchecked((int)0x8A15010D) => InstallOutcome.AlreadyInstalled,
        _ => null,
    };
}
