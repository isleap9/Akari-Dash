using System.Globalization;
using System.Text;

namespace AkariDash.Core.Gpus;

/// <summary>
/// What the GPUs page reads from a monitor's EDID, the way Vain Toolbox reads it (REA): its name
/// (FUN_140183190) and its key under nvlddmkm\State\DisplayDatabase, where the driver keeps the monitor's
/// ScalingConfig (FUN_1401b5b50 builds the key, FUN_1401b60c0 falls back to a near match).
/// </summary>
public static class Edid
{
    private const int BlockLength = 128;
    private static readonly int[] Descriptors = [0x36, 0x48, 0x5A, 0x6C];

    /// <summary>The monitor name descriptor (tag 0xFC), or null when the EDID has none.</summary>
    public static string? MonitorName(byte[] edid)
    {
        if (edid.Length < BlockLength)
        {
            return null;
        }

        foreach (var at in Descriptors)
        {
            if (edid[at] == 0 && edid[at + 1] == 0 && edid[at + 2] == 0 && edid[at + 3] == 0xFC)
            {
                return Text(edid, at);
            }
        }

        return null;
    }

    /// <summary>
    /// The driver's key for this monitor: PnP id, product code, serial (the 0xFF descriptor, or else the
    /// 32-bit serial number), then week, year and the EDID checksum, as "%s%04X%s_%02X_%04X_%02X".
    /// </summary>
    public static string? DisplayDatabaseKey(byte[] edid)
    {
        if (edid.Length < BlockLength)
        {
            return null;
        }

        var pnp = new string([Letter(edid[8] >> 2), Letter(((edid[8] & 0x3) << 3) | (edid[9] >> 5)), Letter(edid[9])]);
        var product = BitConverter.ToUInt16(edid, 10);
        var serial = Descriptors
            .Where(at => edid[at] == 0 && edid[at + 1] == 0 && edid[at + 3] == 0xFF)
            .Select(at => Text(edid, at))
            .FirstOrDefault(text => text.Length > 0)
            ?? BitConverter.ToUInt32(edid, 12).ToString(CultureInfo.InvariantCulture);

        return $"{pnp}{product:X4}{serial}_{edid[0x10]:X2}_{edid[0x11] + 1990:X4}_{edid[0x7F]:X2}";
    }

    /// <summary>
    /// The DisplayDatabase key to use: <paramref name="candidate"/> when it holds a valid ScalingConfig,
    /// otherwise the one key that starts with its first 7 characters and ends with its last 11 and holds a
    /// valid ScalingConfig; null when none or several do.
    /// </summary>
    public static string? FindDisplayDatabaseKey(string candidate, IEnumerable<string> keys, Func<string, bool> hasValidScalingConfig)
    {
        if (hasValidScalingConfig(candidate))
        {
            return candidate;
        }

        var start = candidate[..Math.Min(7, candidate.Length)];
        var end = candidate.Length >= 11 ? candidate[^11..] : candidate;
        var matches = keys
            .Where(key => key.Length >= start.Length + end.Length &&
                          key.StartsWith(start, StringComparison.OrdinalIgnoreCase) &&
                          key.EndsWith(end, StringComparison.OrdinalIgnoreCase) &&
                          hasValidScalingConfig(key))
            .Take(2)
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static char Letter(int value) => (char)('@' + (value & 0x1F));

    // A descriptor's text: up to 13 characters from its fifth byte, ending at a newline, trailing spaces cut.
    private static string Text(byte[] edid, int at)
    {
        var text = new StringBuilder();
        for (var i = 0; i < 13; i++)
        {
            var c = edid[at + 5 + i];
            if (c is 0 or (byte)'\n')
            {
                break;
            }

            text.Append((char)c);
        }

        return text.ToString().TrimEnd(' ');
    }
}
