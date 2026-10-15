namespace AkariDash.Core.Apps;

/// <summary>What winget's exit code means for an install.</summary>
public static class WingetInstallResult
{
    /// <summary>
    /// How the install ended, or <see langword="null"/> when the exit code is a real failure and the
    /// caller should throw with winget's own last words. Codes from winget's published error list:
    /// 3010 and 1641 are the Windows "succeeded, restart needed" pair, and 0x8A150061 is
    /// APPINSTALLER_CLI_ERROR_PACKAGE_ALREADY_INSTALLED.
    /// </summary>
    public static InstallOutcome? Interpret(int exitCode) => exitCode switch
    {
        0 => InstallOutcome.Installed,
        3010 or 1641 => InstallOutcome.InstalledNeedsRestart,
        unchecked((int)0x8A150061) => InstallOutcome.AlreadyInstalled,
        _ => null,
    };
}
