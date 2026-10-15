using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public class WingetInstallResultTests
{
    [Fact]
    public void Zero_is_installed() => Assert.Equal(InstallOutcome.Installed, WingetInstallResult.Interpret(0));

    // ERROR_SUCCESS_REBOOT_REQUIRED and ERROR_SUCCESS_REBOOT_INITIATED: the runtimes return these.
    [Theory]
    [InlineData(3010)]
    [InlineData(1641)]
    public void A_restart_code_is_still_installed(int exitCode) =>
        Assert.Equal(InstallOutcome.InstalledNeedsRestart, WingetInstallResult.Interpret(exitCode));

    // APPINSTALLER_CLI_ERROR_PACKAGE_ALREADY_INSTALLED, which arrives as a negative int.
    [Fact]
    public void Already_installed_is_not_a_failure() =>
        Assert.Equal(InstallOutcome.AlreadyInstalled, WingetInstallResult.Interpret(unchecked((int)0x8A150061)));

    [Theory]
    [InlineData(1)]
    [InlineData(unchecked((int)0x8A15002B))] // no applicable installer found
    [InlineData(-1)]
    public void Anything_else_is_a_failure(int exitCode) => Assert.Null(WingetInstallResult.Interpret(exitCode));

    // What winget itself returns when an installer succeeds but Windows wants a restart:
    // APPINSTALLER_CLI_ERROR_INSTALL_REBOOT_REQUIRED_TO_FINISH and ..._REBOOT_INITIATED. The
    // runtimes in the catalogue return the first of these. 3010/1641 are the raw installer codes
    // winget turns into these, not what it hands back to us.
    [Theory]
    [InlineData(unchecked((int)0x8A150109))]
    [InlineData(unchecked((int)0x8A15010B))]
    public void Wingets_own_restart_codes_are_still_installed(int exitCode) =>
        Assert.Equal(InstallOutcome.InstalledNeedsRestart, WingetInstallResult.Interpret(exitCode));

    // APPINSTALLER_CLI_ERROR_INSTALL_ALREADY_INSTALLED: a version of it is on the machine already,
    // which for this page is a finished install, not a failure.
    [Fact]
    public void Another_version_already_installed_is_not_a_failure() =>
        Assert.Equal(InstallOutcome.AlreadyInstalled, WingetInstallResult.Interpret(unchecked((int)0x8A15010D)));

    // APPINSTALLER_CLI_ERROR_INSTALL_REBOOT_REQUIRED_FOR_INSTALL: nothing was installed, and the
    // user has to restart before it can be. That one really is a failure.
    [Fact]
    public void Restart_required_before_installing_is_a_failure() =>
        Assert.Null(WingetInstallResult.Interpret(unchecked((int)0x8A15010A)));
}
