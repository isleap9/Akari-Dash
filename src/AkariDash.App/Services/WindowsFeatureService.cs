using System.Diagnostics;
using System.Text.Json;
using AkariDash.Core.Apps;

namespace AkariDash.App.Services;

/// <summary>
/// Windows features through WMI (for their names as Turn Windows features on or off shows them) and
/// capabilities through dism.exe; every change through dism.exe, which is what AkariOS-Ultimate's
/// Disable-WindowsOptionalFeature and Remove-WindowsCapability run underneath. Elevated, as Akari-Dash is.
/// </summary>
public sealed class WindowsFeatureService : IWindowsFeatureService
{
    public async Task<IReadOnlyList<WindowsFeature>> ListAsync(CancellationToken cancellationToken = default)
    {
        var features = ListFeaturesAsync(cancellationToken);
        var capabilities = ListCapabilitiesAsync(cancellationToken);
        return [.. await features, .. await capabilities];
    }

    public Task<bool> TurnOnAsync(WindowsFeature feature, CancellationToken cancellationToken = default) =>
        ChangeAsync(feature, feature.Kind == WindowsFeatureKind.Feature
            ? ["/Enable-Feature", $"/FeatureName:{feature.Name}", "/All"]
            : ["/Add-Capability", $"/CapabilityName:{feature.Name}"], cancellationToken);

    public Task<bool> TurnOffAsync(WindowsFeature feature, CancellationToken cancellationToken = default) =>
        ChangeAsync(feature, feature.Kind == WindowsFeatureKind.Feature
            ? ["/Disable-Feature", $"/FeatureName:{feature.Name}"]
            : ["/Remove-Capability", $"/CapabilityName:{feature.Name}"], cancellationToken);

    private static async Task<bool> ChangeAsync(WindowsFeature feature, string[] change, CancellationToken cancellationToken)
    {
        var (exitCode, output) = await DismAsync([.. change, "/NoRestart", "/Quiet"], cancellationToken);
        if (exitCode is 0 or DismOutput.RestartRequired)
        {
            return exitCode == DismOutput.RestartRequired;
        }

        var reason = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(line => line.StartsWith("Error", StringComparison.OrdinalIgnoreCase) || line.Contains("failed", StringComparison.OrdinalIgnoreCase));
        throw new InvalidOperationException(
            $"Could not change {feature.DisplayName} (DISM exit code {exitCode}){(reason is null ? "." : $": {reason}")}");
    }

    private static async Task<IReadOnlyList<WindowsFeature>> ListFeaturesAsync(CancellationToken cancellationToken)
    {
        // InstallState: 1 enabled, 2 disabled, 3 absent (its files removed); only 1 is on.
        const string query =
            "Get-CimInstance Win32_OptionalFeature | Select-Object Name, Caption, InstallState | ConvertTo-Json -Compress";
        var json = await RunAsync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", query], cancellationToken);

        List<WindowsFeature> features = [];
        using var document = JsonDocument.Parse(json.Output.Trim() is { Length: > 0 } text ? text : "[]");
        var rows = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToList()
            : [document.RootElement];
        foreach (var row in rows)
        {
            var name = row.GetProperty("Name").GetString() ?? string.Empty;
            var caption = row.TryGetProperty("Caption", out var c) ? c.GetString() : null;
            features.Add(new WindowsFeature(
                name,
                string.IsNullOrWhiteSpace(caption) ? name : caption,
                WindowsFeatureKind.Feature,
                row.GetProperty("InstallState").GetInt32() == 1));
        }

        return [.. features.OrderBy(feature => feature.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }

    private static async Task<IReadOnlyList<WindowsFeature>> ListCapabilitiesAsync(CancellationToken cancellationToken)
    {
        var (_, output) = await DismAsync(["/Get-Capabilities", "/Format:Table"], cancellationToken);
        return [.. DismOutput.Capabilities(output).OrderBy(capability => capability.DisplayName, StringComparer.OrdinalIgnoreCase)];
    }

    private static Task<(int ExitCode, string Output)> DismAsync(string[] arguments, CancellationToken cancellationToken) =>
        RunAsync(Path.Combine(Environment.SystemDirectory, "dism.exe"), ["/English", "/Online", .. arguments], cancellationToken);

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

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{Path.GetFileName(file)} did not start.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await output);
    }
}
