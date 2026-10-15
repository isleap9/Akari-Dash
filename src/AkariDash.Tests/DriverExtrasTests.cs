using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class DriverExtrasTests
{
    private static IEnumerable<DriverExtra> Every =>
        Enum.GetValues<GpuVendor>().SelectMany(vendor =>
            Enum.GetValues<DriverStepKind>().SelectMany(kind => DriverExtras.For(vendor, kind)));

    // The configured install and the debloat both put the settings on, exactly once, as AkariOS does.
    [Theory]
    [InlineData(GpuVendor.Nvidia, DriverStepKind.InstallAndConfigure)]
    [InlineData(GpuVendor.Nvidia, DriverStepKind.Debloat)]
    [InlineData(GpuVendor.Amd, DriverStepKind.InstallAndConfigure)]
    [InlineData(GpuVendor.Amd, DriverStepKind.Debloat)]
    [InlineData(GpuVendor.Intel, DriverStepKind.InstallAndConfigure)]
    [InlineData(GpuVendor.Intel, DriverStepKind.Debloat)]
    public void A_configuring_row_applies_the_settings_once(GpuVendor vendor, DriverStepKind kind) =>
        Assert.Single(DriverExtras.For(vendor, kind).OfType<ApplyRegistrySettings>());

    [Theory]
    [InlineData(DriverStepKind.Install)]
    [InlineData(DriverStepKind.Clean)]
    [InlineData(DriverStepKind.CleanManual)]
    public void The_other_rows_do_nothing_more(DriverStepKind kind) =>
        Assert.All(Enum.GetValues<GpuVendor>(), vendor => Assert.Empty(DriverExtras.For(vendor, kind)));

    // AkariOS's NVIDIA entries: the Control Panel package, then Profile Inspector, then the profile.
    [Fact]
    public void NVIDIA_gets_its_Control_Panel_and_Profile_Inspector_before_the_profile()
    {
        var extras = DriverExtras.For(GpuVendor.Nvidia, DriverStepKind.InstallAndConfigure).ToList();

        var appx = extras.FindIndex(extra => extra is InstallAppx);
        var inspector = extras.FindIndex(extra => extra is DownloadTool);
        var profile = extras.FindIndex(extra => extra is ImportNvidiaProfile);

        Assert.True(appx >= 0 && inspector > appx && profile > inspector);
        Assert.Contains(extras, extra => extra is CreateShortcut { Target: DriverExtras.NvidiaControlPanelAppId });
        Assert.Equal("the NVIDIA Control Panel and Nvidia Profile Inspector",
            DriverExtras.Downloads(GpuVendor.Nvidia, DriverStepKind.Debloat));
    }

    // The debloat goes further than the configured install, never the other way round.
    [Fact]
    public void Only_the_debloat_deletes_services() =>
        Assert.All(Enum.GetValues<GpuVendor>(), vendor =>
            Assert.DoesNotContain(DriverExtras.For(vendor, DriverStepKind.InstallAndConfigure), extra => extra is RemoveService));

    [Fact]
    public void Everything_is_fetched_over_https()
    {
        Assert.All(Every.OfType<InstallAppx>(), appx => Assert.Equal("https", appx.Source.Scheme));
        Assert.All(Every.OfType<DownloadTool>(), tool => Assert.Equal("https", tool.Source.Scheme));
    }

    // A path that removes something must be rooted in a named place, never the drive's root or a bare name.
    [Fact]
    public void Every_removal_names_a_place_below_a_known_root()
    {
        Assert.All(Every.OfType<RemovePath>(), remove =>
        {
            Assert.Matches(@"^%(SystemDrive|ProgramData|SystemRoot|ProgramFiles\(x86\))%\\.+", remove.Path);
            Assert.DoesNotContain("..", remove.Path);
        });
    }

    [Fact]
    public void Every_step_says_what_it_does() =>
        Assert.All(Every, extra => Assert.False(string.IsNullOrWhiteSpace(extra.Describe())));

    [Fact]
    public void Intel_s_debloat_also_installs_its_control_app_from_the_package()
    {
        var plan = DriverDebloat.Plan(GpuVendor.Intel, DriverDebloat.InstallerNameFor(GpuVendor.Intel));

        Assert.NotNull(plan);
        Assert.StartsWith(@"Resources\Extras\IntelGraphicsSoftware_", plan.AfterSetup);
        Assert.Equal(["/s"], plan.AfterSetupArguments);
    }
}
