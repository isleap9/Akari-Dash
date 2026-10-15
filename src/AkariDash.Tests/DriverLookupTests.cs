using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class DriverLookupTests
{
    // A trimmed copy of the live AjaxDriverService answer (2026-10-15). Version and URL are checked
    // against each other, so a newer driver does not fail the test.
    private const string NvidiaAnswer = """
        { "Success" : "1", "IDS" : [{ "downloadInfo": { "Success" : "1", "ID" : "280103",
        "Name" : "GeForce%20Game%20Ready%20Driver", "Release" : "615", "Version" : "617.42", "IsWHQL" : "1", "IsDC" : "1",
        "DownloadURL" : "https://us.download.nvidia.com/Windows/617.42/617.42-desktop-win10-win11-64bit-international-dch-whql.exe" } }] }
        """;

    [Fact]
    public void Nvidia_reads_the_version_and_its_download_url()
    {
        var driver = DriverLookup.Parse(GpuVendor.Nvidia, NvidiaAnswer);

        Assert.NotNull(driver);
        Assert.Equal("617.42", driver.Version);
        Assert.Contains(driver.Version, driver.DownloadUrl.AbsoluteUri);  // the URL names the version
        Assert.Equal("https", driver.DownloadUrl.Scheme);
    }

    // Review Focus 1: a consent page, an empty body, or an error must read as "could not ask".
    [Theory]
    [InlineData("<html><body>Access denied</body></html>")]
    [InlineData("")]
    [InlineData("""{"IDS":[]}""")]
    public void Nvidia_reads_nothing_from_anything_else(string body) =>
        Assert.Null(DriverLookup.Parse(GpuVendor.Nvidia, body));

    // The live page's markup, trimmed (2026-10-15).
    private const string AmdAnswer = """
        <a class="btn" target="_blank" href="https://drivers.amd.com/drivers/installer/26.10/whql/amd-software-adrenalin-edition-26.8.1-minimalsetup-260818_web.exe" data-x="1">Download</a>
        """;

    [Fact]
    public void Amd_reads_the_minimalsetup_link()
    {
        var driver = DriverLookup.Parse(GpuVendor.Amd, AmdAnswer);

        Assert.NotNull(driver);
        Assert.Equal("26.8.1", driver.Version);                            // read out of the file name
        Assert.EndsWith("_web.exe", driver.DownloadUrl.AbsoluteUri);
    }

    // Review Focus 1: a link on a lookalike host is not AMD's installer.
    [Theory]
    [InlineData("""<a href="https://example.com/amd-software-adrenalin-edition-25.3.1-minimalsetup-1_web.exe">x</a>""")]
    [InlineData("""<a href="https://example.com/?u=https://drivers.amd.com/drivers/installer/26.10/whql/amd-software-adrenalin-edition-26.8.1-minimalsetup-1_web.exe">x</a>""")]
    public void Amd_ignores_a_link_that_is_not_on_drivers_amd_com(string body) =>
        Assert.Null(DriverLookup.Parse(GpuVendor.Amd, body));

    // Intel has no feed we can parse; the row hands off to the page instead.
    [Fact]
    public void Intel_has_a_page_but_no_parse() =>
        Assert.Null(DriverLookup.Parse(GpuVendor.Intel, "<html></html>"));

    [Fact]
    public void Every_vendor_has_an_https_endpoint() =>
        Assert.All(Enum.GetValues<GpuVendor>(),
            vendor => Assert.Equal("https", DriverLookup.Endpoint(vendor).Scheme));
}
