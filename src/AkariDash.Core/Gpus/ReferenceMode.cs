using System.Globalization;

namespace AkariDash.Core.Gpus;

/// <summary>
/// "Override to Reference Mode" as Vain Toolbox writes it (REA, FUN_1401b7030 and FUN_1401b6b10): the DWORD
/// <see cref="ValueName"/> in HKCU\<see cref="DevicesKey"/>\&lt;device&gt;\Color, 2 for on and 1 for off,
/// for every device key named after the display id in decimal and a dash.
/// </summary>
public static class ReferenceMode
{
    public const string DevicesKey = @"SOFTWARE\NVIDIA Corporation\Global\NVTweak\Devices";
    public const string ValueName = "3538970";

    /// <summary>The device keys of <paramref name="displayId"/>, or "&lt;id&gt;-0" when it has none yet.</summary>
    public static IReadOnlyList<string> Devices(uint displayId, IEnumerable<string> keys)
    {
        var prefix = displayId.ToString(CultureInfo.InvariantCulture) + "-";
        var devices = keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        return devices.Count > 0 ? devices : [prefix + "0"];
    }

    public static int ValueFor(bool on) => on ? 2 : 1;

    public static bool IsOn(int? value) => value == 2;
}
