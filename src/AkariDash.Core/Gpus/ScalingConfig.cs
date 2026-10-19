namespace AkariDash.Core.Gpus;

/// <summary>
/// The NVIDIA driver's ScalingConfig value under nvlddmkm\State\DisplayDatabase\&lt;monitor&gt;, where it keeps
/// "Override Scaling Mode Set by Games and Programs". Read out of Vain Toolbox with REA (FUN_1401b5ed0,
/// FUN_1401b6570): 16 bytes, 0x2DB and 0x10 as the first two DWORDs, the override bits in byte 8, and the
/// sum of bytes 0-11 as the last DWORD. Vain only ever edits a blob the driver already wrote.
/// </summary>
public static class ScalingConfig
{
    private const int Length = 16;

    public static bool IsValid(byte[] blob) =>
        blob.Length == Length &&
        BitConverter.ToUInt32(blob, 0) == 0x2DB &&
        BitConverter.ToUInt32(blob, 4) == 0x10 &&
        BitConverter.ToUInt32(blob, 12) == Sum(blob);

    /// <summary>Byte 8: zero when games may pick their own scaling.</summary>
    public static byte OverrideBits(byte[] blob) => blob[8];

    /// <summary>A copy of <paramref name="blob"/> with byte 8 set to <paramref name="bits"/> and the sum redone.</summary>
    public static byte[] WithOverrideBits(byte[] blob, byte bits)
    {
        var written = (byte[])blob.Clone();
        written[8] = bits;
        BitConverter.TryWriteBytes(written.AsSpan(12), Sum(written));
        return written;
    }

    /// <summary>
    /// The bits Vain Toolbox writes for an NV_SCALING value: one bit per mode when overriding games, none
    /// otherwise, except integer scaling, which always keeps its bit.
    /// </summary>
    public static byte OverrideBitsFor(int scaling, bool overrideGames) =>
        (byte)(!overrideGames && scaling != 8 ? 0 : scaling switch
        {
            1 => 0x08,
            2 => 0x01,
            3 => 0x04,
            5 => 0x02,
            6 => 0x10,
            7 => 0x20,
            8 => 0x40,
            _ => 0,
        });

    private static uint Sum(byte[] blob)
    {
        uint sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += blob[i];
        }

        return sum;
    }
}
