using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class DriverVersionTests
{
    private static AvailableDriver Driver(string version) => new(version, new Uri("https://example.com/d.exe"));

    [Fact]
    public void An_older_installed_driver_has_an_update() =>
        Assert.Equal(DriverVersionState.UpdateAvailable, DriverVersion.State("566.36", Driver("572.16")));

    [Fact]
    public void The_same_driver_is_up_to_date() =>
        Assert.Equal(DriverVersionState.UpToDate, DriverVersion.State("566.36", Driver("566.36")));

    [Fact]
    public void A_newer_installed_driver_is_not_an_update() =>
        Assert.Equal(DriverVersionState.UpToDate, DriverVersion.State("572.16", Driver("566.36")));

    // AMD's numbering has different-length parts; 25.3 is older than 25.3.1.
    [Fact]
    public void A_shorter_installed_version_is_older() =>
        Assert.Equal(DriverVersionState.UpdateAvailable, DriverVersion.State("25.3", Driver("25.3.1")));

    // Review Focus 2: what we cannot compare we must not call either way.
    [Theory]
    [InlineData(null, "566.36")]
    [InlineData("566.36", null)]
    [InlineData("", "566.36")]
    [InlineData("unknown", "566.36")]
    [InlineData("566.36", "")]
    [InlineData("566.36", "unknown")]
    public void Anything_we_cannot_read_is_unknown(string? installed, string? available) =>
        Assert.Equal(DriverVersionState.Unknown, DriverVersion.State(installed, available is null ? null : Driver(available)));

    [Fact]
    public void Two_unreadable_versions_are_unknown() =>
        Assert.Equal(DriverVersionState.Unknown, DriverVersion.State(null, null));

    // Windows' DriverVersion is not the number the vendor shows; compare like with like.
    [Theory]
    [InlineData(GpuVendor.Nvidia, "32.0.15.6636", null, "566.36")]
    [InlineData(GpuVendor.Nvidia, "32.0.16.1742", null, "617.42")]
    [InlineData(GpuVendor.Nvidia, "31.0.15.3623", null, "536.23")]
    [InlineData(GpuVendor.Intel, "32.0.101.6083", null, "101.6083")]
    [InlineData(GpuVendor.Amd, "32.0.21001.9024", "26.8.1", "26.8.1")]
    public void The_installed_version_is_read_the_way_the_vendor_writes_it(
        GpuVendor vendor, string driverVersion, string? radeon, string expected) =>
        Assert.Equal(expected, DriverVersion.FromWindows(vendor, driverVersion, radeon));

    [Theory]
    [InlineData(GpuVendor.Nvidia, "566.36", null)]
    [InlineData(GpuVendor.Nvidia, "", null)]
    [InlineData(GpuVendor.Intel, null, null)]
    [InlineData(GpuVendor.Amd, "32.0.21001.9024", null)]   // no Adrenalin number recorded
    public void An_unexpected_shape_reads_as_nothing(GpuVendor vendor, string? driverVersion, string? radeon) =>
        Assert.Null(DriverVersion.FromWindows(vendor, driverVersion, radeon));
}
