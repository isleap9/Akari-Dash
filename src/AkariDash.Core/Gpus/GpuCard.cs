using System.Globalization;
using AkariDash.Core.Machine;

namespace AkariDash.Core.Gpus;

/// <summary>Whether a card is part of the processor or a card of its own.</summary>
public enum GpuKind
{
    Integrated,
    Discrete,
}

/// <summary>
/// What Windows holds about one display adapter, as read: SetupAPI's instance id and PCIe link
/// properties, and the values of its key under the display class. Values Windows does not have are null.
/// </summary>
public sealed record GpuAdapterRaw(
    string InstanceId,
    string? MatchingDeviceId,
    string? DriverDesc,
    string? DriverVersion,
    string? DriverDate,
    object? QwMemorySize,
    object? MemorySize,
    uint? CurrentLinkSpeed,
    uint? CurrentLinkWidth,
    uint? MaxLinkSpeed,
    uint? MaxLinkWidth);

/// <summary>
/// One graphics card as the GPUs page shows it, after Vain Toolbox's GPU page: name, maker, integrated
/// or discrete, driver, video memory and PCIe link. Only NVIDIA, AMD and Intel cards are cards here.
/// </summary>
public sealed record GpuCard(
    string InstanceId,
    string Name,
    GpuVendor Vendor,
    GpuKind Kind,
    string? DriverVersion,
    DateOnly? DriverDate,
    string? VideoMemory,
    string? PcieLink)
{
    /// <summary>The card <paramref name="raw"/> describes, or null for any other adapter.</summary>
    public static GpuCard? From(GpuAdapterRaw raw)
    {
        if (GpuVendorExtensions.FromMatchingDeviceId(raw.MatchingDeviceId) is not { } vendor)
        {
            return null;
        }

        var name = string.IsNullOrWhiteSpace(raw.DriverDesc) ? $"{vendor.DisplayName()} graphics" : raw.DriverDesc.Trim();
        return new GpuCard(
            raw.InstanceId,
            name,
            vendor,
            KindOf(vendor, name),
            Version(vendor, raw.DriverVersion),
            Date(raw.DriverDate),
            Memory(raw.QwMemorySize) ?? Memory(raw.MemorySize),
            Link(raw.CurrentLinkSpeed, raw.CurrentLinkWidth, raw.MaxLinkSpeed, raw.MaxLinkWidth));
    }

    // Intel's are part of the processor except Arc; AMD's processor graphics are named "... Graphics"
    // (Radeon(TM) Graphics, Radeon 780M Graphics) where its cards carry RX and a model number.
    private static GpuKind KindOf(GpuVendor vendor, string name) => vendor switch
    {
        GpuVendor.Intel => name.Contains("Arc", StringComparison.OrdinalIgnoreCase) ? GpuKind.Discrete : GpuKind.Integrated,
        GpuVendor.Amd => name.EndsWith("Graphics", StringComparison.OrdinalIgnoreCase) ? GpuKind.Integrated : GpuKind.Discrete,
        _ => GpuKind.Discrete,
    };

    // NVIDIA's own number is the last five digits of Windows' version: 32.0.15.6094 is 560.94.
    private static string? Version(GpuVendor vendor, string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var digits = new string(version.Where(char.IsDigit).ToArray());
        return vendor == GpuVendor.Nvidia && digits.Length >= 5
            ? $"{digits[^5..^2]}.{digits[^2..]} ({version})"
            : version;
    }

    private static DateOnly? Date(string? date) =>
        DateOnly.TryParseExact(date, "M-d-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;

    private static string? Memory(object? value)
    {
        long bytes = value switch
        {
            long qword => qword,
            int dword => (uint)dword,
            uint dword => dword,
            byte[] { Length: >= 8 } data => BitConverter.ToInt64(data),
            byte[] { Length: >= 4 } data => BitConverter.ToUInt32(data),
            _ => 0,
        };

        if (bytes <= 0)
        {
            return null;
        }

        const long gb = 1L << 30;
        return bytes >= gb
            ? $"{Math.Round((double)bytes / gb, 1).ToString("0.#", CultureInfo.InvariantCulture)} GB"
            : $"{bytes >> 20} MB";
    }

    // DEVPKEY_PciDevice_*LinkSpeed: 1 is 2.5 GT/s (PCIe 1.0) up to 5, 32 GT/s (PCIe 5.0).
    private static string? Link(uint? speed, uint? width, uint? maxSpeed, uint? maxWidth)
    {
        if (speed is not (>= 1 and <= 6) || width is null or 0)
        {
            return null;
        }

        var now = $"PCIe {speed}.0 x{width}";
        if (maxSpeed is not (>= 1 and <= 6) || maxWidth is null or 0)
        {
            return now;
        }

        var max = $"PCIe {maxSpeed}.0 x{maxWidth}";
        return now == max ? now : $"{now} (supports {max})";
    }
}
