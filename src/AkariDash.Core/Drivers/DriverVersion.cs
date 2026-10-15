using System.Globalization;
using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>Whether the driver on a card is current, as far as we can tell.</summary>
public enum DriverVersionState
{
    /// <summary>One side could not be read or compared. Not the same as up to date.</summary>
    Unknown,
    UpToDate,
    UpdateAvailable,
}

/// <summary>
/// Compares the installed driver with the one the vendor offers. "We could not tell" and "it is
/// current" are different things a person acts on differently, so anything unreadable is Unknown.
/// </summary>
public static class DriverVersion
{
    public static DriverVersionState State(string? installed, AvailableDriver? available)
    {
        if (Parts(installed) is not { } have || Parts(available?.Version) is not { } offered)
        {
            return DriverVersionState.Unknown;
        }

        for (var i = 0; i < Math.Max(have.Length, offered.Length); i++)
        {
            var mine = i < have.Length ? have[i] : 0;
            var theirs = i < offered.Length ? offered[i] : 0;
            if (mine != theirs)
            {
                return mine < theirs ? DriverVersionState.UpdateAvailable : DriverVersionState.UpToDate;
            }
        }

        return DriverVersionState.UpToDate;
    }

    /// <summary>
    /// The version as the vendor writes it, from what Windows records for the adapter. Windows'
    /// own <c>DriverVersion</c> is not the vendor's number: NVIDIA's 32.0.15.6636 is 566.36 (the
    /// last five digits), Intel's 32.0.101.6083 is 101.6083 (the last two parts), and AMD's
    /// Adrenalin number is a separate value, <c>RadeonSoftwareVersion</c>. <c>null</c> when the
    /// recorded value does not have the shape we expect.
    /// </summary>
    public static string? FromWindows(GpuVendor vendor, string? driverVersion, string? radeonSoftwareVersion = null)
    {
        if (vendor == GpuVendor.Amd)
        {
            return Parts(radeonSoftwareVersion) is null ? null : radeonSoftwareVersion!.Trim();
        }

        if (Parts(driverVersion) is not { Length: 4 } parts)
        {
            return null;
        }

        if (vendor == GpuVendor.Intel)
        {
            return $"{parts[2]}.{parts[3]}";
        }

        var digits = $"{parts[2]}{parts[3]:D4}";
        if (digits.Length < 5)
        {
            return null;
        }

        var last = digits[^5..];
        return $"{int.Parse(last[..3], CultureInfo.InvariantCulture)}.{last[3..]}";
    }

    private static int[]? Parts(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var parts = version.Trim().Split('.');
        var numbers = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return null;
            }
        }

        return numbers;
    }
}
