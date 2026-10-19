using System.Text;
using AkariDash.Core.Gpus;
using Xunit;

namespace AkariDash.Tests;

/// <summary>
/// A monitor's name and its key under nvlddmkm\State\DisplayDatabase, read from its EDID the way Vain
/// Toolbox reads them (FUN_140183190, FUN_1401b5b50, FUN_1401b60c0). The EDID below is built by hand:
/// PnP id GSM (0x1E6D), product 0x5B7F, serial 0x01010101, week 30, year 2021, checksum 0xA3.
/// </summary>
public class EdidTests
{
    private static byte[] Build(string? name = "LG ULTRAGEAR", string? serial = "105NTABC1234")
    {
        var edid = new byte[128];
        byte[] header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];
        header.CopyTo(edid, 0);
        edid[8] = 0x1E;
        edid[9] = 0x6D;
        edid[10] = 0x7F;
        edid[11] = 0x5B;
        edid[12] = edid[13] = edid[14] = edid[15] = 0x01;
        edid[0x10] = 0x1E;
        edid[0x11] = 31;
        edid[0x7F] = 0xA3;
        if (name is not null)
        {
            Descriptor(edid, 0x5A, 0xFC, name);
        }

        if (serial is not null)
        {
            Descriptor(edid, 0x6C, 0xFF, serial);
        }

        return edid;
    }

    private static void Descriptor(byte[] edid, int at, byte tag, string text)
    {
        edid[at + 3] = tag;
        var bytes = Encoding.ASCII.GetBytes((text + "\n").PadRight(13));
        bytes.CopyTo(edid, at + 5);
    }

    [Fact]
    public void The_name_is_the_monitor_name_descriptor()
    {
        Assert.Equal("LG ULTRAGEAR", Edid.MonitorName(Build()));
    }

    [Fact]
    public void Without_a_name_descriptor_there_is_no_name()
    {
        Assert.Null(Edid.MonitorName(Build(name: null)));
    }

    [Fact]
    public void The_key_is_pnp_id_product_serial_week_year_and_checksum()
    {
        Assert.Equal("GSM5B7F105NTABC1234_1E_07E5_A3", Edid.DisplayDatabaseKey(Build()));
    }

    [Fact]
    public void Without_a_serial_descriptor_the_key_carries_the_serial_number()
    {
        Assert.Equal("GSM5B7F16843009_1E_07E5_A3", Edid.DisplayDatabaseKey(Build(serial: null)));
    }

    [Fact]
    public void A_short_edid_has_no_key_or_name()
    {
        Assert.Null(Edid.DisplayDatabaseKey(new byte[64]));
        Assert.Null(Edid.MonitorName(new byte[64]));
    }

    [Fact]
    public void The_key_is_used_as_is_when_the_driver_has_it()
    {
        var key = Edid.FindDisplayDatabaseKey("GSM5B7F105NTABC1234_1E_07E5_A3", ["OTHER"], _ => true);

        Assert.Equal("GSM5B7F105NTABC1234_1E_07E5_A3", key);
    }

    [Fact]
    public void Otherwise_the_one_key_with_the_same_start_and_end_is_used()
    {
        // The first 7 characters and the last 11 must match, and the key must hold a valid ScalingConfig.
        string[] keys = ["GSM5B7FXYZ_1E_07E5_A3", "AUS27A1123_1E_07E5_A3", "GSM5B7F999_1E_07E5_A4"];

        var key = Edid.FindDisplayDatabaseKey("GSM5B7F105NTABC1234_1E_07E5_A3", keys, name => name != "GSM5B7F105NTABC1234_1E_07E5_A3");

        Assert.Equal("GSM5B7FXYZ_1E_07E5_A3", key);
    }

    [Fact]
    public void Two_matching_keys_are_no_key()
    {
        string[] keys = ["GSM5B7FXYZ_1E_07E5_A3", "gsm5b7fABC_1e_07e5_a3"];

        Assert.Null(Edid.FindDisplayDatabaseKey("GSM5B7F105NTABC1234_1E_07E5_A3", keys, name => name.Length < 25));
    }
}
