using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class DriverInstallCommandTests
{
    private static DriverRow Row(GpuVendor vendor, DriverStepKind kind) =>
        new($"{vendor}".ToLowerInvariant() + ".x", vendor, kind, "Title", "Description.", DriverRisk.Caution);

    // Graphics.ps1:269 - NVIDIA's own flags, kept exactly.
    [Fact]
    public void Nvidia_installs_silently_without_rebooting_or_asking()
    {
        var arguments = DriverInstallCommand.Install(Row(GpuVendor.Nvidia, DriverStepKind.Install), "setup.exe");

        Assert.Contains("-s", arguments);
        Assert.Contains("-noreboot", arguments);
        Assert.Contains("-noeula", arguments);
        Assert.DoesNotContain("-clean", arguments);           // the clean install is the configured row
    }

    [Fact]
    public void The_configured_row_asks_NVIDIA_for_a_clean_install() =>
        Assert.Contains("-clean",
            DriverInstallCommand.Install(Row(GpuVendor.Nvidia, DriverStepKind.InstallAndConfigure), "setup.exe"));

    // If we cannot install a vendor unattended, we must not emit a bare path that opens a wizard
    // while the page claims to be working: no arguments, and IsUnattended says so.
    [Fact]
    public void Only_NVIDIA_can_be_installed_unattended()
    {
        Assert.True(DriverInstallCommand.IsUnattended(GpuVendor.Nvidia));
        Assert.False(DriverInstallCommand.IsUnattended(GpuVendor.Amd));
        Assert.False(DriverInstallCommand.IsUnattended(GpuVendor.Intel));
        Assert.Empty(DriverInstallCommand.Install(Row(GpuVendor.Amd, DriverStepKind.Install), "setup.exe"));
        Assert.Empty(DriverInstallCommand.Install(Row(GpuVendor.Intel, DriverStepKind.Install), "setup.exe"));
    }

    [Fact]
    public void Describe_is_the_line_a_person_would_type() =>
        Assert.Equal(
            "C:\\Temp\\setup.exe -s -noreboot",
            DriverInstallCommand.Describe("C:\\Temp\\setup.exe", ["-s", "-noreboot"]));

    [Fact]
    public void Describe_quotes_a_path_with_a_space() =>
        Assert.Equal(
            "\"C:\\Program Files\\setup.exe\" -s",
            DriverInstallCommand.Describe("C:\\Program Files\\setup.exe", ["-s"]));
}
