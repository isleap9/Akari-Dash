using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class DriverDebloatTests
{
    [Fact]
    public void An_nvidia_installer_gets_NVIDIA_s_strip_list()
    {
        var plan = DriverDebloat.Plan(GpuVendor.Nvidia, "566.36-desktop-win10-win11-64bit-international-dch-whql.exe");

        Assert.NotNull(plan);
        Assert.NotEmpty(plan.RemoveAfterExtracting);
    }

    [Fact]
    public void Every_vendor_recognises_its_own_installer() =>
        Assert.All(Enum.GetValues<GpuVendor>(),
            vendor => Assert.NotNull(DriverDebloat.Plan(vendor, DriverDebloat.InstallerNameFor(vendor))));

    // Review Focus 4: another vendor's file, or anything unknown, is refused outright.
    [Theory]
    [InlineData(GpuVendor.Nvidia, "amd-software-adrenalin-edition-25.3.1-minimalsetup-1_web.exe")]
    [InlineData(GpuVendor.Amd, "566.36-desktop-win10-win11-64bit-international-dch-whql.exe")]
    [InlineData(GpuVendor.Amd, "amd-software-adrenalin-edition-26.8.1-minimalsetup-260818_web.exe")] // nothing to strip
    [InlineData(GpuVendor.Intel, "566.36-desktop-win10-win11-64bit-international-dch-whql.exe")]
    [InlineData(GpuVendor.Nvidia, "totally-unrelated-installer.exe")]
    [InlineData(GpuVendor.Nvidia, "readme.txt")]
    public void A_file_that_is_not_that_vendor_s_installer_is_refused(GpuVendor vendor, string fileName) =>
        Assert.Null(DriverDebloat.Plan(vendor, fileName));

    // Nothing in a plan may reach outside the folder we extracted into.
    [Fact]
    public void No_plan_entry_leaves_the_extraction_folder()
    {
        Assert.All(Enum.GetValues<GpuVendor>(), vendor =>
        {
            var plan = DriverDebloat.Plan(vendor, DriverDebloat.InstallerNameFor(vendor));
            Assert.NotNull(plan);

            Assert.All(plan.RemoveAfterExtracting.Concat(plan.TurnOffInConfig).Append(plan.Setup), entry =>
            {
                Assert.False(string.IsNullOrWhiteSpace(entry));
                Assert.False(Path.IsPathRooted(entry));
                Assert.DoesNotContain("..", entry);
            });
        });
    }

    [Fact]
    public void Turning_off_an_xml_component_disables_and_unhides_it() =>
        Assert.Equal(
            "<Enabled>false</Enabled><Hidden>false</Hidden>",
            DriverDebloat.TurnOff("AMDUpdater.xml", "<Enabled>true</Enabled><Hidden>true</Hidden>"));

    [Fact]
    public void Turning_off_a_json_manifest_stops_its_default_install() =>
        Assert.Equal(
            """{ "InstallByDefault" : "No" }""",
            DriverDebloat.TurnOff("InstallManifest.json", """{ "InstallByDefault":"Yes" }"""));

    [Fact]
    public void The_inbox_lists_only_executables_and_never_folders()
    {
        var found = DriverInbox.Installers(
            ["C:\\x\\driver.exe", "C:\\x\\notes.txt", "C:\\x\\sub", "C:\\x\\setup.EXE"]);

        Assert.Equal(2, found.Count);
        Assert.All(found, path => Assert.EndsWith(".exe", path, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_inbox_lives_under_the_user_s_Documents() =>
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            DriverInbox.Folder,
            StringComparison.OrdinalIgnoreCase);
}
