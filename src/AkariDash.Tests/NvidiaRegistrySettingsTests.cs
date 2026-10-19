using AkariDash.Core.Gpus;
using AkariDash.Core.Machine;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class NvidiaRegistrySettingsTests
{
    private const string CardKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000";

    private static NvidiaRegistrySetting Find(string name) => NvidiaRegistrySettings.All.Single(setting => setting.Name == name);

    [Fact]
    public void Power_management_is_vain_toolboxs_list_in_its_order()
    {
        var power = NvidiaRegistrySettings.All.Where(setting => setting.Group == "Power Management").ToList();

        Assert.Equal(45, power.Count);
        Assert.Equal("RmPowerFeature", power[0].Name);
        Assert.Equal("RmPowerFeature2", power[1].Name);
        Assert.Equal("RMElpgStateOnInit", power[^1].Name);
        Assert.Equal(power.Count, power.Select(setting => setting.Name).Distinct().Count());
    }

    [Theory]
    [InlineData("RmPowerFeature", 0x54455565u)]
    [InlineData("RmPowerFeature2", 0x05555555u)]
    [InlineData("DisableDynamicPstate", 1u)]
    [InlineData("RMElcg", 0x55555554u)]
    [InlineData("RMGC6Feature", 0x000AAAAAu)]
    [InlineData("RMDidleFeatureGC5", 0x02AA8AAAu)]
    [InlineData("RMClkSlowDown", 0x04400000u)]
    [InlineData("RmClkPowerOffDramPllWhenUnused", 0u)]
    [InlineData("RMLpwrArch", 0x00055555u)]
    [InlineData("RM303107", 0x10u)]
    [InlineData("RMOPSB", 0x2AA2u)]
    [InlineData("RmDisableInforomBBX", 0xFu)]
    [InlineData("RmPgCtrlParameters", 0x55555155u)]
    [InlineData("RMElpgStateOnInit", 3u)]
    public void Each_value_is_the_one_vain_recommends(string name, uint recommended)
    {
        Assert.Equal(recommended, Find(name).Recommended);
    }

    [Fact]
    public void A_setting_lives_under_the_cards_own_display_class_key()
    {
        Assert.Equal(
            new RegistryLocation(RegistryHive.LocalMachine, CardKey, "RMElcg"),
            Find("RMElcg").Location("0000"));
    }

    [Fact]
    public void Hd_audio_d3_cold_lives_under_the_nvlddmkm_service_instead()
    {
        Assert.Equal(
            new RegistryLocation(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\nvlddmkm", "EnableHDAudioD3Cold"),
            Find("EnableHDAudioD3Cold").Location("0000"));
    }

    [Theory]
    [InlineData("05555555", 0x05555555u)]
    [InlineData("0x2AA2", 0x2AA2u)]
    [InlineData(" aaaaa ", 0xAAAAAu)]
    [InlineData("FFFFFFFF", 0xFFFFFFFFu)]
    public void Typed_values_are_read_as_hexadecimal(string typed, uint value)
    {
        Assert.Equal(value, NvidiaRegistrySettings.ParseHex(typed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789")]
    [InlineData("xyz")]
    public void Anything_else_is_not_a_value(string typed)
    {
        Assert.Null(NvidiaRegistrySettings.ParseHex(typed));
    }

    [Fact]
    public void Values_show_as_eight_hex_digits_as_vain_shows_them()
    {
        Assert.Equal("000AAAAA", NvidiaRegistrySettings.FormatHex(0xAAAAA));
    }
}
