using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class DriverRowsTests
{
    [Fact]
    public void Every_row_is_listed_once()
    {
        Assert.Equal(
            DriverRows.All.Count,
            DriverRows.All.Select(row => row.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_row_is_complete()
    {
        Assert.All(DriverRows.All, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Id));
            Assert.False(string.IsNullOrWhiteSpace(row.Title));
            Assert.False(string.IsNullOrWhiteSpace(row.Description));
            Assert.EndsWith(".", row.Description);          // one sentence, in RestorableApps' voice
            Assert.Matches("^[a-z0-9.-]+$", row.Id);        // e.g. nvidia.install-and-configure
            Assert.StartsWith(row.Vendor.ToString().ToLowerInvariant(), row.Id);
        });
    }

    [Fact]
    public void Every_vendor_has_the_three_rows()
    {
        Assert.All(Enum.GetValues<GpuVendor>(), vendor =>
        {
            var rows = DriverRows.For(vendor);
            Assert.Equal(3, rows.Count);
            Assert.All(rows, row => Assert.Equal(vendor, row.Vendor));
            Assert.Equal(
                [DriverStepKind.Install, DriverStepKind.InstallAndConfigure, DriverStepKind.Debloat],
                rows.Select(row => row.Kind));
        });
    }

    // Review Focus 3: an unknown vendor (or no card) must yield nothing, not every row.
    [Fact]
    public void A_vendor_with_no_rows_yields_none() =>
        Assert.Empty(DriverRows.For((GpuVendor)999));
}
