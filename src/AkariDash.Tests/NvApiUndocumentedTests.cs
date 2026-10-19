using System.Runtime.InteropServices;
using AkariDash.Core.Gpus.NvApi;
using Xunit;

namespace AkariDash.Tests;

/// <summary>
/// The NVAPI functions nvapi.h does not publish, which the Settings tab uses for Digital Vibrance, Hue and
/// Brightness/Contrast/Gamma (the user's call, 2026-10-19). Ids and struct versions are the ones Vain
/// Toolbox passes, read out of its binary with REA (mov ecx,&lt;id&gt; before nvapi_QueryInterface, and the
/// version it writes into each struct).
/// </summary>
public class NvApiUndocumentedTests
{
    [Theory]
    [InlineData(NvApiUndocumented.GetDVCInfoEx, 0x0e45002du)]
    [InlineData(NvApiUndocumented.SetDVCLevelEx, 0x4a82c2b1u)]
    [InlineData(NvApiUndocumented.GetHUEInfo, 0x95b64341u)]
    [InlineData(NvApiUndocumented.SetHUEAngle, 0xf5a0f22cu)]
    [InlineData(NvApiUndocumented.SetColorTable, 0x7082a053u)]
    public void Ids_match_what_vain_toolbox_calls(uint id, uint expected)
    {
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData(typeof(NvDisplayDvcInfoEx), 20, 0x00010014u)]
    [InlineData(typeof(NvDisplayHueInfo), 12, 0x0001000cu)]
    [InlineData(typeof(NvColorTable), 0x3008, 0x00013008u)]
    public void Structs_have_the_versions_vain_toolbox_passes(Type type, int size, uint version)
    {
        Assert.Equal(size, Marshal.SizeOf(type));
        Assert.Equal(version, (uint)type.GetField("Version")!.GetValue(null)!);
    }

    [Theory]
    [InlineData(typeof(NvDisplayDvcInfoEx), "CurrentLevel", 4)]
    [InlineData(typeof(NvDisplayDvcInfoEx), "MaxLevel", 12)]
    [InlineData(typeof(NvDisplayHueInfo), "CurrentAngle", 4)]
    [InlineData(typeof(NvColorTable), "Enabled", 0x3004)]
    public void Fields_sit_where_vain_toolbox_reads_them(Type type, string field, int offset)
    {
        Assert.Equal(offset, (int)Marshal.OffsetOf(type, field));
    }
}
