using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class GraphicsEntriesTests
{
    // AkariOS-Ultimate's Graphics category, in its order (Tweaks/Graphics.ps1).
    [Fact]
    public void The_entries_are_AkariOS_s_in_AkariOS_s_order() =>
        Assert.Equal(
            [
                "driver-clean", "driver-install", "driver-install-settings", "driver-debloat",
                "nvidia-settings", "amd-settings", "intel-settings", "hdcp", "p0-state", "msi-mode",
                "directx", "cpp", "resolution", "hags-windowed",
            ],
            GraphicsEntries.All.Select(entry => entry.Id));

    [Fact]
    public void Every_toggle_has_an_Optimize_and_a_Default() =>
        Assert.All(GraphicsEntries.All.Where(entry => entry.Kind == GraphicsEntryKind.Toggle), entry =>
        {
            Assert.NotEmpty(GraphicsEntries.Apply(entry.Id));
            Assert.NotEmpty(GraphicsEntries.Revert(entry.Id));
        });

    [Fact]
    public void Every_action_does_something_and_has_no_Default() =>
        Assert.All(GraphicsEntries.All.Where(entry => entry.Kind == GraphicsEntryKind.Action), entry =>
        {
            Assert.NotEmpty(GraphicsEntries.Apply(entry.Id));
            Assert.Empty(GraphicsEntries.Revert(entry.Id));
        });

    // The driver groups: NVIDIA, AMD, Intel each, and DDU's two modes.
    [Fact]
    public void Every_driver_group_offers_each_vendor()
    {
        foreach (var id in new[] { "driver-install", "driver-install-settings", "driver-debloat" })
        {
            var entry = GraphicsEntries.All.Single(entry => entry.Id == id);
            Assert.Equal(GraphicsEntryKind.Group, entry.Kind);
            Assert.Equal([GpuVendor.Nvidia, GpuVendor.Amd, GpuVendor.Intel], entry.Choices.Select(choice => choice.Vendor));
        }

        var ddu = GraphicsEntries.All.Single(entry => entry.Id == "driver-clean");
        Assert.Equal([DriverStepKind.Clean, DriverStepKind.CleanManual], ddu.Choices.Select(choice => choice.Kind));
        Assert.All(ddu.Choices, choice => Assert.False(string.IsNullOrWhiteSpace(choice.Confirm)));
    }

    // NVIDIA settings is where the profile and the Control Panel come from without a driver install.
    [Fact]
    public void NVIDIA_settings_installs_the_Control_Panel_and_imports_the_profile()
    {
        var optimize = GraphicsEntries.Apply("nvidia-settings");

        Assert.Contains(optimize, step => step is InstallAppx);
        Assert.Contains(optimize, step => step is ImportNvidiaProfile { Reset: false });
        Assert.Contains(GraphicsEntries.Revert("nvidia-settings"), step => step is ImportNvidiaProfile { Reset: true });
    }

    [Fact]
    public void HDCP_and_P0_flip_one_value_each_way()
    {
        Assert.Equal("1", Assert.IsType<SetRegistry>(Assert.Single(GraphicsEntries.Apply("hdcp"))).Setting.Value);
        Assert.Equal("0", Assert.IsType<SetRegistry>(Assert.Single(GraphicsEntries.Revert("hdcp"))).Setting.Value);
        Assert.Equal("DisableDynamicPstate", Assert.IsType<SetRegistry>(Assert.Single(GraphicsEntries.Apply("p0-state"))).Setting.Name);
    }

    [Fact]
    public void Every_registry_value_in_the_entries_matches_its_type()
    {
        var settings = GraphicsEntries.All
            .SelectMany(entry => GraphicsEntries.Apply(entry.Id).Concat(GraphicsEntries.Revert(entry.Id)))
            .OfType<SetRegistry>()
            .Select(step => step.Setting);

        Assert.All(settings, setting =>
        {
            Assert.DoesNotContain("..", setting.KeyPath);
            Assert.DoesNotContain("ControlSet", setting.KeyPath);
            switch (setting.Kind)
            {
                case DriverSettingKind.Dword:
                    Assert.True(uint.TryParse(setting.Value, out _));
                    break;
                case DriverSettingKind.Binary:
                    Assert.Matches(@"^([0-9A-Fa-f]{2})+$", setting.Value);
                    break;
            }
        });
    }

    [Fact]
    public void Every_download_is_https() =>
        Assert.All(
            GraphicsEntries.All.SelectMany(entry => GraphicsEntries.Apply(entry.Id).Concat(GraphicsEntries.Revert(entry.Id))),
            step =>
            {
                var source = step switch
                {
                    InstallAppx appx => appx.Source,
                    DownloadTool tool => tool.Source,
                    InstallRedistributable redist => redist.Source,
                    InstallDirectX directx => directx.Source,
                    _ => null,
                };
                if (source is not null)
                {
                    Assert.Equal("https", source.Scheme);
                }
            });

    [Fact]
    public void Twelve_runtimes_as_AkariOS_installs_them() =>
        Assert.Equal(12, GraphicsEntries.Apply("cpp").OfType<InstallRedistributable>().Count());
}
