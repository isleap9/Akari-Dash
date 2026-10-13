using AkariDash.Core.Apps;
using Microsoft.Win32;

namespace AkariDash.App.Services;

/// <summary>
/// Removes and reinstalls legacy apps with AkariOS-Ultimate's own scripts (see <see cref="EmbeddedScripts"/>),
/// and tells whether each is on this PC by what the script removes.
/// </summary>
public sealed class WindowsLegacyAppService : ILegacyAppService
{
    private static string X86(string path) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), path);

    private static string System32(string path) => Path.Combine(Environment.SystemDirectory, path);

    public bool IsInstalled(LegacyApp app) => Detect(app);

    /// <summary>Whether the program is here, read off what its removal script takes away.</summary>
    public static bool Detect(LegacyApp app) => app.Id switch
    {
        "edge" => File.Exists(X86(@"Microsoft\Edge\Application\msedge.exe")),
        "webview" => Directory.Exists(X86(@"Microsoft\EdgeWebView\Application")),
        "onedrive" =>
            File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\OneDrive\OneDrive.exe")) ||
            File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft OneDrive\OneDrive.exe")) ||
            File.Exists(X86(@"Microsoft OneDrive\OneDrive.exe")),
        "remote-desktop" => File.Exists(System32("mstsc.exe")),
        "snipping-tool" => File.Exists(System32("SnippingTool.exe")),
        "brlapi" => Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "brltty")) ||
            Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\brlapi") is { } service && Dispose(service),
        _ => HasUninstallEntry(app),
    };

    public static bool IsWindows10 => Environment.OSVersion.Version.Build < 22000;

    public async Task RemoveAsync(LegacyApp app, CancellationToken cancellationToken = default)
    {
        var errors = await EmbeddedScripts.RunAsync(app.RemoveScript, cancellationToken);

        // The scripts carry on past what fails, as AkariOS-Ultimate's do, so the result is read off the
        // machine rather than the exit code.
        if (Detect(app))
        {
            throw new InvalidOperationException($"Could not uninstall {app.Name}.{FirstLine(errors)}");
        }
    }

    public async Task InstallAsync(LegacyApp app, CancellationToken cancellationToken = default)
    {
        if (app.InstallScript is null)
        {
            throw new InvalidOperationException($"{app.Name} has no installer here.");
        }

        var errors = await EmbeddedScripts.RunAsync(app.InstallScript, cancellationToken);
        if (!Detect(app))
        {
            throw new InvalidOperationException($"{app.Name}'s installer finished, but {app.Name} is not installed.{FirstLine(errors)}");
        }
    }

    private static bool HasUninstallEntry(LegacyApp app)
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var uninstall = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view)
                .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            foreach (var name in uninstall?.GetSubKeyNames() ?? [])
            {
                using var entry = uninstall!.OpenSubKey(name);
                if (entry?.GetValue("DisplayName") is string displayName && app.Matches(displayName.Trim()))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool Dispose(RegistryKey key)
    {
        key.Dispose();
        return true;
    }

    private static string FirstLine(string text) =>
        text.Trim() is { Length: > 0 } trimmed ? $" {trimmed.Split('\n')[0].Trim()}" : string.Empty;
}
