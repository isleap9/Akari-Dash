using AkariDash.Core.Gpus;
using Xunit;

namespace AkariDash.Tests;

/// <summary>
/// The driver's ScalingConfig value (nvlddmkm\State\DisplayDatabase\&lt;monitor&gt;), as Vain Toolbox reads and
/// writes it: 16 bytes, 0x2DB and 0x10 first, the override bits in byte 8, and the sum of bytes 0-11 last.
/// </summary>
public class ScalingConfigTests
{
    // DB 02 00 00 | 10 00 00 00 | 00 00 00 00 | ED 00 00 00: 0xDB + 0x02 + 0x10 = 0xED.
    private static readonly byte[] Plain =
        [0xDB, 0x02, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xED, 0x00, 0x00, 0x00];

    [Fact]
    public void A_blob_with_the_header_and_its_sum_is_valid()
    {
        Assert.True(ScalingConfig.IsValid(Plain));
    }

    [Fact]
    public void A_blob_whose_sum_is_off_is_not()
    {
        var broken = (byte[])Plain.Clone();
        broken[12] = 0xEE;

        Assert.False(ScalingConfig.IsValid(broken));
    }

    [Fact]
    public void Setting_the_override_bits_writes_byte_8_and_a_new_sum()
    {
        var written = ScalingConfig.WithOverrideBits(Plain, 0x20);

        Assert.Equal(
            [0xDB, 0x02, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x0D, 0x01, 0x00, 0x00],
            written);
        Assert.Equal(0x20, ScalingConfig.OverrideBits(written));
        Assert.Equal(0x00, Plain[8]);
    }

    // Vain Toolbox's switch in FUN_1401cc130, one bit per NV_SCALING value from nvapi.h.
    [Theory]
    [InlineData(1, 0x08)]
    [InlineData(2, 0x01)]
    [InlineData(3, 0x04)]
    [InlineData(5, 0x02)]
    [InlineData(6, 0x10)]
    [InlineData(7, 0x20)]
    [InlineData(8, 0x40)]
    public void Overriding_sets_the_bit_of_the_scaling_mode(int scaling, int bits)
    {
        Assert.Equal(bits, ScalingConfig.OverrideBitsFor(scaling, overrideGames: true));
    }

    [Fact]
    public void Without_override_the_bits_are_clear_except_for_integer_scaling()
    {
        Assert.Equal(0x00, ScalingConfig.OverrideBitsFor(5, overrideGames: false));
        Assert.Equal(0x40, ScalingConfig.OverrideBitsFor(8, overrideGames: false));
    }

    [Fact]
    public void A_blob_of_another_size_or_header_is_not()
    {
        Assert.False(ScalingConfig.IsValid(Plain[..12]));

        var other = (byte[])Plain.Clone();
        other[0] = 0xDC;
        other[12] = 0xEE;
        Assert.False(ScalingConfig.IsValid(other));
    }
}
