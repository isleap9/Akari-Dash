using System.Text.Json;
using System.Text.RegularExpressions;
using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>
/// Where each vendor says what its current driver is, and how to read the answer. Every parser
/// returns <c>null</c> for anything it does not recognise - a consent page, an error, an empty
/// body - so a broken source reads as "could not check", never as a plausible wrong download.
/// </summary>
public static partial class DriverLookup
{
    // NVIDIA's product series and family ids in the GeForce lookup (AkariOS's pair). They pick the
    // desktop Game Ready line; the DCH WHQL driver it returns is the same package for every
    // current GeForce desktop card. Checked live 2026-10-15: 617.42.
    private const int NvidiaProductSeriesId = 120;
    private const int NvidiaProductFamilyId = 929;

    public static Uri NvidiaEndpoint { get; } = new(
        "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php" +
        $"?func=DriverManualLookup&psid={NvidiaProductSeriesId}&pfid={NvidiaProductFamilyId}" +
        "&osID=57&languageCode=1033&isWHQL=1&dch=1&sort1=0&numberOfResults=1");

    public static Uri AmdEndpoint { get; } = new("https://www.amd.com/en/support/download/drivers.html");

    // Intel publishes no feed we can read; its row opens this search page instead.
    public static Uri IntelEndpoint { get; } = new(
        "https://www.intel.com/content/www/us/en/search.html#sortCriteria=%40lastmodifieddt%20descending" +
        "&f-operatingsystem_en=Windows%2011%20Family*&f-downloadtype=Drivers&cf-tabfilter=Downloads" +
        "&cf-downloadsppth=Graphics");

    public static Uri Endpoint(GpuVendor vendor) => vendor switch
    {
        GpuVendor.Nvidia => NvidiaEndpoint,
        GpuVendor.Amd => AmdEndpoint,
        GpuVendor.Intel => IntelEndpoint,
        _ => throw new ArgumentOutOfRangeException(nameof(vendor), vendor, null),
    };

    public static AvailableDriver? Parse(GpuVendor vendor, string body) => vendor switch
    {
        GpuVendor.Nvidia => ParseNvidia(body),
        GpuVendor.Amd => ParseAmd(body),
        _ => null,
    };

    private static AvailableDriver? ParseNvidia(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("IDS", out var ids)
                || ids.ValueKind != JsonValueKind.Array
                || ids.GetArrayLength() == 0
                || !ids[0].TryGetProperty("downloadInfo", out var info)
                || !info.TryGetProperty("Version", out var versionElement)
                || versionElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var version = versionElement.GetString();
            if (string.IsNullOrWhiteSpace(version) || !NvidiaVersion().IsMatch(version))
            {
                return null;
            }

            // Built the way AkariOS builds it (Graphics.ps1:213): the 64-bit DCH WHQL package.
            return new AvailableDriver(version, new Uri(
                $"https://international.download.nvidia.com/Windows/{version}/{version}-desktop-win10-win11-64bit-international-dch-whql.exe"));
        }
        catch (Exception exception) when (exception is JsonException or UriFormatException)
        {
            return null;
        }
    }

    private static AvailableDriver? ParseAmd(string html)
    {
        var match = AmdInstallerLink().Match(html);
        if (!match.Success
            || !Uri.TryCreate(match.Groups["url"].Value, UriKind.Absolute, out var url)
            || !string.Equals(url.Host, "drivers.amd.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new AvailableDriver(match.Groups["version"].Value, url);
    }

    [GeneratedRegex(@"^[0-9]+(\.[0-9]+)+$")]
    private static partial Regex NvidiaVersion();

    // Graphics.ps1:227's pattern, anchored to the start of an attribute value so the host is
    // drivers.amd.com itself and not a substring of some other link.
    [GeneratedRegex(
        @"href\s*=\s*[""'](?<url>https://drivers\.amd\.com/drivers/installer/[^""'\s<>]*/whql/amd-software-adrenalin-edition-(?<version>[0-9]+(?:\.[0-9]+)+)-minimalsetup-[^""'\s<>/]*_web\.exe)[""']",
        RegexOptions.IgnoreCase)]
    private static partial Regex AmdInstallerLink();
}
