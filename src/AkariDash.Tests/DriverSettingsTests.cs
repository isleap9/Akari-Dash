using System.Xml.Linq;
using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class DriverSettingsTests
{
    private static IEnumerable<DriverSetting> Every => Enum.GetValues<GpuVendor>().SelectMany(DriverSettings.For);

    [Fact]
    public void Every_vendor_has_settings() =>
        Assert.All(Enum.GetValues<GpuVendor>(), vendor => Assert.NotEmpty(DriverSettings.For(vendor)));

    [Fact]
    public void Every_setting_is_complete()
    {
        Assert.All(Every, setting =>
        {
            Assert.True(Enum.IsDefined(setting.Root));
            Assert.True(Enum.IsDefined(setting.Kind));
            Assert.False(string.IsNullOrWhiteSpace(setting.Name));
            Assert.False(string.IsNullOrWhiteSpace(setting.Value));

            // Only an adapter-key setting may name the key itself with an empty path.
            if (setting.Root != DriverSettingRoot.GraphicsClass)
            {
                Assert.False(string.IsNullOrWhiteSpace(setting.KeyPath));
            }
        });
    }

    // Each value is written with the type it declares, so the service never has to guess one.
    [Fact]
    public void Every_value_matches_the_type_it_declares()
    {
        Assert.All(Every, setting =>
        {
            switch (setting.Kind)
            {
                case DriverSettingKind.Dword:
                    Assert.Matches(@"^[0-9]+$", setting.Value);
                    Assert.True(uint.TryParse(setting.Value, out _));
                    break;
                case DriverSettingKind.Binary:
                    Assert.Matches(@"^([0-9A-Fa-f]{2})+$", setting.Value);   // whole bytes, never a half
                    break;
                case DriverSettingKind.String:
                    Assert.DoesNotContain('\0', setting.Value);
                    break;
            }
        });
    }

    // Review Focus 5: a path that escaped its root would write somewhere else entirely.
    [Fact]
    public void Every_key_path_is_relative_and_stays_inside_its_root()
    {
        Assert.All(Every, setting =>
        {
            Assert.DoesNotContain("..", setting.KeyPath);
            Assert.DoesNotMatch(@"^[A-Za-z]:", setting.KeyPath);
            Assert.False(setting.KeyPath.StartsWith('\\'));
            Assert.DoesNotContain("HKEY", setting.KeyPath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ControlSet", setting.KeyPath);   // choosing the control set is the service's job
        });
    }

    // XDocument.Parse throws on malformed XML, which is the point: a broken profile stops here
    // rather than reaching an installer.
    [Fact]
    public void The_NVIDIA_profile_is_well_formed_and_carries_settings()
    {
        var profile = XDocument.Parse(DriverSettings.NvidiaProfileXml);

        Assert.NotEmpty(profile.Descendants("ProfileSetting"));
    }
}
