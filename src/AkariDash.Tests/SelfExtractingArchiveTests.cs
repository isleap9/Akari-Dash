using AkariDash.Core.Drivers;
using Xunit;

namespace AkariDash.Tests;

public class SelfExtractingArchiveTests
{
    private static readonly byte[] Signature = [(byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C];

    private static MemoryStream With(int prefix, bool signature = true)
    {
        var bytes = new byte[prefix].Concat(signature ? Signature : []).Concat(new byte[32]).ToArray();
        new Random(7).NextBytes(bytes.AsSpan(0, prefix));
        bytes.AsSpan(0, prefix).Replace((byte)'7', (byte)'8');   // no stray signature in the noise
        return new MemoryStream(bytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1024)]
    [InlineData(65533)]   // straddles the first read
    [InlineData(200_000)]
    public void Finds_the_archive_after_the_program(int prefix) =>
        Assert.Equal(prefix, SelfExtractingArchive.FindSevenZipStart(With(prefix)));

    [Fact]
    public void A_file_with_no_archive_has_no_start() =>
        Assert.Null(SelfExtractingArchive.FindSevenZipStart(With(4096, signature: false)));
}
