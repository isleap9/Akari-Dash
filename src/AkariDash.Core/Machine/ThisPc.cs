using System.Globalization;

namespace AkariDash.Core.Machine;

/// <summary>
/// The "This PC" summary shown on Home: <see cref="PcFacts"/> made readable. A value Windows could
/// not report is <see langword="null"/>. A static snapshot, never live monitoring (ADR-0002).
/// </summary>
public sealed class ThisPc(PcFacts facts)
{
    // The first Windows 11 build; Windows 11 still calls itself "Windows 10" in its product name.
    private const int FirstWindows11Build = 22000;

    /// <summary>Edition, feature update and build, e.g. <c>Windows 11 Pro 24H2 (build 26100.2033)</c>.</summary>
    public string? Os { get; } = DescribeOs(facts);

    public string? Cpu { get; } = Tidy(facts.Cpu);

    /// <summary>Every graphics card, once each, comma-separated.</summary>
    public string? Gpu { get; } = facts.Gpus
        .Select(Tidy)
        .OfType<string>()
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList() is { Count: > 0 } gpus ? string.Join(", ", gpus) : null;

    /// <summary>Installed memory in gigabytes, e.g. <c>16 GB</c> or <c>15.9 GB</c>.</summary>
    public string? Ram { get; } = facts.InstalledRamKilobytes is { } kilobytes
        ? $"{Math.Round(kilobytes / (1024.0 * 1024.0), 1).ToString("0.#", CultureInfo.InvariantCulture)} GB"
        : null;

    /// <summary>Maker and model, e.g. <c>ASUSTeK COMPUTER INC. ROG STRIX B550-F GAMING</c>.</summary>
    public string? Motherboard { get; } = DescribeBoard(facts);

    /// <summary>Every disk with its size, e.g. <c>Samsung SSD 980 PRO 1TB (1 TB)</c>, comma-separated.</summary>
    public string? Storage { get; } = (facts.Disks ?? [])
        .Select(disk => Tidy(disk.Model) is { } model
            ? disk.Bytes is { } bytes ? $"{model} ({DescribeSize(bytes)})" : model
            : null)
        .OfType<string>()
        .ToList() is { Count: > 0 } disks ? string.Join(", ", disks) : null;

    /// <summary>What each card draws now, once each, e.g. <c>2560 × 1440 @ 165 Hz</c>.</summary>
    public string? Display { get; } = (facts.Displays ?? [])
        .Where(display => display.Width > 0 && display.Height > 0)
        .Select(display => display.RefreshRate is > 1 and var hz
            ? $"{display.Width} × {display.Height} @ {hz} Hz"
            : $"{display.Width} × {display.Height}")
        .Distinct()
        .ToList() is { Count: > 0 } displays ? string.Join(", ", displays) : null;

    public string? RunningAs { get; } = facts.RunningAs;

    public string? SignedIn { get; } = facts.SignedIn;

    /// <summary>
    /// Akari-Dash runs elevated as an account other than the signed-in user, so per-user Tweaks
    /// would apply to that account instead. False when either account is unknown.
    /// </summary>
    public bool RunsAsOtherAccount { get; } =
        facts.RunningAs is not null && facts.SignedIn is not null &&
        !string.Equals(facts.RunningAs, facts.SignedIn, StringComparison.OrdinalIgnoreCase);

    private static string? DescribeOs(PcFacts facts)
    {
        var name = Tidy(facts.ProductName);
        if (name is not null && facts.Build >= FirstWindows11Build && name.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase))
        {
            name = "Windows 11" + name["Windows 10".Length..];
        }

        var build = facts.Build is { } number
            ? $"(build {number}{(facts.Revision is { } revision ? $".{revision}" : string.Empty)})"
            : null;

        var parts = new[] { name, Tidy(facts.DisplayVersion), build }.OfType<string>().ToList();
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    // Board makers leave these placeholders in the firmware instead of a name.
    private static readonly string[] FirmwarePlaceholders =
        ["To be filled by O.E.M.", "Default string", "System manufacturer", "System Product Name", "Not Applicable"];

    private static string? DescribeBoard(PcFacts facts)
    {
        var parts = new[] { Tidy(facts.BoardMaker), Tidy(facts.BoardModel) }
            .Where(part => part is not null && !FirmwarePlaceholders.Contains(part, StringComparer.OrdinalIgnoreCase))
            .ToList();
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    // Decimal units, as disk makers label them: a "1 TB" drive reports 1,000,204,886,016 bytes.
    private static string DescribeSize(ulong bytes) =>
        bytes >= 1_000_000_000_000
            ? $"{Math.Round(bytes / 1e12, 1).ToString("0.#", CultureInfo.InvariantCulture)} TB"
            : $"{Math.Round(bytes / 1e9).ToString("0", CultureInfo.InvariantCulture)} GB";

    private static string? Tidy(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : string.Join(" ", text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
