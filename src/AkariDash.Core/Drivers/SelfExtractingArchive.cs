namespace AkariDash.Core.Drivers;

/// <summary>
/// The vendors ship their drivers as 7-Zip self-extracting executables: a small program, then the
/// archive. Reading the archive means finding where it starts, which is its six-byte signature.
/// </summary>
public static class SelfExtractingArchive
{
    private static readonly byte[] SevenZipSignature = [(byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C];

    /// <summary>
    /// Where the 7-Zip archive inside <paramref name="stream"/> begins, or <see langword="null"/>
    /// when there is none, so a file that is not one is refused rather than guessed at.
    /// </summary>
    public static long? FindSevenZipStart(Stream stream)
    {
        const int bufferSize = 1 << 16;
        var buffer = new byte[bufferSize + SevenZipSignature.Length - 1];
        var carried = 0;
        long position = stream.Position;

        while (true)
        {
            var read = stream.Read(buffer, carried, bufferSize);
            if (read == 0)
            {
                return null;
            }

            var length = carried + read;
            var index = buffer.AsSpan(0, length).IndexOf(SevenZipSignature);
            if (index >= 0)
            {
                return position - carried + index;
            }

            // Keep the tail, in case the signature straddles two reads.
            carried = Math.Min(SevenZipSignature.Length - 1, length);
            Array.Copy(buffer, length - carried, buffer, 0, carried);
            position += read;
        }
    }
}
