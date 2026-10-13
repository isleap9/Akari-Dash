using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AkariDash.Core.Apps;

namespace AkariDash.App.Services;

/// <summary>
/// Driver packages from DISM's driver list (Get-WindowsDriver), named after the devices that use them
/// (Win32_PnPSignedDriver); disconnected devices from Get-PnpDevice. Every change through pnputil.exe.
/// Elevated, as Akari-Dash is.
/// </summary>
public sealed class WindowsSystemCleanupService : ISystemCleanupService
{
    public async Task<IReadOnlyList<DriverPackage>> ListDriversAsync(CancellationToken cancellationToken = default)
    {
        const string script = """
            $devices = @{}
            Get-CimInstance Win32_PnPSignedDriver | Where-Object { $_.InfName -like 'oem*.inf' -and $_.DeviceName } |
                ForEach-Object { if (-not $devices.ContainsKey($_.InfName)) { $devices[$_.InfName] = $_.DeviceName } }
            Get-WindowsDriver -Online | ForEach-Object {
                [pscustomobject]@{
                    Published = $_.Driver
                    Original  = [IO.Path]::GetFileName($_.OriginalFileName)
                    Device    = $devices[$_.Driver]
                    Class     = $_.ClassName
                    Provider  = $_.ProviderName
                    Version   = $_.Version
                    Date      = if ($_.Date) { $_.Date.ToString('yyyy-MM-dd') } else { $null }
                }
            } | ConvertTo-Json -Compress
            """;

        List<DriverPackage> drivers = [];
        foreach (var row in await PowerShellJsonAsync(script, cancellationToken))
        {
            drivers.Add(new DriverPackage(
                Text(row, "Published"),
                Text(row, "Original"),
                row.TryGetProperty("Device", out var device) ? device.GetString() : null,
                Text(row, "Class"),
                Text(row, "Provider"),
                Text(row, "Version"),
                DateTime.TryParseExact(Text(row, "Date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null));
        }

        return [.. drivers.OrderBy(driver => driver.ClassName, StringComparer.OrdinalIgnoreCase).ThenBy(driver => driver.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task DeleteDriverAsync(DriverPackage driver, CancellationToken cancellationToken = default)
    {
        // /uninstall takes it off the devices using it first; without /force, Windows refuses when a
        // device still needs it, and that refusal is reported rather than overridden.
        var (exitCode, output) = await RunAsync("pnputil.exe", ["/delete-driver", driver.PublishedName, "/uninstall"], cancellationToken);
        if (exitCode is not (0 or 3010))
        {
            throw new InvalidOperationException($"Could not delete {driver.DisplayName} ({driver.PublishedName}): {LastLine(output)}");
        }
    }

    public async Task ExportDriverAsync(DriverPackage driver, string folder, CancellationToken cancellationToken = default)
    {
        var target = Path.Combine(folder, Path.GetFileNameWithoutExtension(driver.OriginalName) is { Length: > 0 } name ? $"{name} ({driver.PublishedName})" : driver.PublishedName);
        Directory.CreateDirectory(target);
        var (exitCode, output) = await RunAsync("pnputil.exe", ["/export-driver", driver.PublishedName, target], cancellationToken);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Could not export {driver.DisplayName} ({driver.PublishedName}): {LastLine(output)}");
        }
    }

    public async Task<IReadOnlyList<GhostDevice>> ListGhostDevicesAsync(CancellationToken cancellationToken = default)
    {
        const string script = """
            Get-PnpDevice | Where-Object { -not $_.Present } |
                Select-Object @{ n = 'Id'; e = { $_.InstanceId } }, @{ n = 'Name'; e = { if ($_.FriendlyName) { $_.FriendlyName } else { $_.Description } } }, Class |
                ConvertTo-Json -Compress
            """;

        List<GhostDevice> devices = [];
        foreach (var row in await PowerShellJsonAsync(script, cancellationToken))
        {
            var id = Text(row, "Id");
            var name = Text(row, "Name");
            devices.Add(new GhostDevice(id, name.Length > 0 ? name : id, Text(row, "Class") is { Length: > 0 } c ? c : "Other"));
        }

        return [.. devices.OrderBy(device => device.ClassName, StringComparer.OrdinalIgnoreCase).ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task RemoveDeviceAsync(GhostDevice device, CancellationToken cancellationToken = default)
    {
        var (exitCode, output) = await RunAsync("pnputil.exe", ["/remove-device", device.InstanceId], cancellationToken);
        if (exitCode is not (0 or 3010))
        {
            throw new InvalidOperationException($"Could not remove {device.Name}: {LastLine(output)}");
        }
    }

    private static string Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;

    private static string LastLine(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "no reason given.";

    private static async Task<IReadOnlyList<JsonElement>> PowerShellJsonAsync(string script, CancellationToken cancellationToken)
    {
        var (_, output) = await RunAsync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", script], cancellationToken);
        var json = output.Trim();
        if (json.Length == 0)
        {
            return [];
        }

        // ConvertTo-Json writes a lone object, not a one-item array, when there is only one.
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return root.ValueKind == JsonValueKind.Array
            ? [.. root.EnumerateArray().Select(row => row.Clone())]
            : [root.Clone()];
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string file, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{file} did not start.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, (await output) + (await errors));
    }
}
