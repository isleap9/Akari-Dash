using AkariDash.Core.Gpus;
using Xunit;

namespace AkariDash.Tests;

/// <summary>
/// "Override to Reference Mode" as Vain Toolbox writes it (FUN_1401b7030, FUN_1401b6b10): the DWORD
/// 3538970 under HKCU\SOFTWARE\NVIDIA Corporation\Global\NVTweak\Devices\&lt;device&gt;\Color, where a
/// display's devices are the subkeys named after its display id in decimal and a dash.
/// </summary>
public class ReferenceModeTests
{
    [Fact]
    public void A_display_s_devices_are_the_keys_named_after_its_id()
    {
        // 0x80061086 is 2147881094 in decimal.
        string[] keys = ["2147881094-0", "2147881094-1", "21478810940-0", "1234-0"];

        Assert.Equal(["2147881094-0", "2147881094-1"], ReferenceMode.Devices(0x80061086, keys));
    }

    [Fact]
    public void Without_a_key_of_its_own_the_display_gets_its_first()
    {
        Assert.Equal(["2147881094-0"], ReferenceMode.Devices(0x80061086, ["1234-0"]));
    }

    [Fact]
    public void On_is_two_and_off_is_one()
    {
        Assert.Equal(2, ReferenceMode.ValueFor(on: true));
        Assert.Equal(1, ReferenceMode.ValueFor(on: false));
        Assert.True(ReferenceMode.IsOn(2));
        Assert.False(ReferenceMode.IsOn(1));
        Assert.False(ReferenceMode.IsOn(null));
    }
}
