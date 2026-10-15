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
}
