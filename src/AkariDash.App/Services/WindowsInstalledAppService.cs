using System.Diagnostics;
using AkariDash.Core.Apps;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace AkariDash.App.Services;

/// <summary>
/// Lists Store (Appx/MSIX) packages via <see cref="PackageManager"/> and classic programs via the
/// Uninstall registry keys, and removes them. Runs elevated (see app.manifest), so per-machine
/// removals do not fail halfway for lack of rights.
/// </summary>
public sealed class WindowsInstalledAppService(ILegacyAppService legacy) : IInstalledAppService
{
    public async Task<IReadOnlyList<InstalledApp>> ListAsync(CancellationToken cancellationToken = default)
    {
        // Registry reads are fast; Appx enumeration touches WinRT, so keep both off the UI thread.
        return await Task.Run(ListOnBackground, cancellationToken);
    }

    private static IReadOnlyList<InstalledApp> ListOnBackground()
    {
        var apps = new List<InstalledApp>();
        apps.AddRange(ListAppx());
        apps.AddRange(ListWin32());
        return apps;
    }

    private static IEnumerable<InstalledApp> ListAppx()
    {
        List<InstalledApp> apps = [];
        PackageManager packages;
        try
        {
            packages = new PackageManager();
        }
        catch
        {
            return apps;
        }

        IEnumerable<Windows.ApplicationModel.Package> found;
        try
        {
            found = packages.FindPackagesForUser(string.Empty);
        }
        catch
        {
            return apps;
        }

        foreach (var package in found)
        {
            try
            {
                // Frameworks, resources and stubs cannot be removed on their own. System-signed packages
                // are pieces of Windows itself - App Resolver, AsyncTextService, the GUID-named shell
                // parts - not apps anyone installed, and most refuse removal anyway. Listing them is what
                // turned thirteen apps into a hundred.
                if (package.IsFramework || package.IsResourcePackage || package.SignatureKind == PackageSignatureKind.System)
                {
                    continue;
                }

                var id = package.Id;
                var name = InstalledAppText.PackageName(package.DisplayName, id.Name);
                var publisher = string.IsNullOrWhiteSpace(package.PublisherDisplayName)
                    ? id.Publisher
                    : package.PublisherDisplayName;
                var version = $"{id.Version.Major}.{id.Version.Minor}.{id.Version.Build}.{id.Version.Revision}";

                apps.Add(new InstalledApp(
                    Id: $"appx:{id.FullName}",
                    Name: name,
                    Publisher: publisher,
                    Version: version,
                    Kind: InstalledAppKind.Appx,
                    PackageFullName: id.FullName,
                    IconPath: LogoPath(package),
                    PackageFamilyName: id.FamilyName));
            }
            catch
            {
                // One unreadable package must never hide every other app.
            }
        }

        return apps.OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase);
    }

    // The package's logo as Windows resolved it for this PC's scale; null when it has none, or when
    // asking throws, which a half-registered package can.
    private static string? LogoPath(Windows.ApplicationModel.Package package)
    {
        try
        {
            return package.Logo is { IsFile: true } logo && File.Exists(logo.LocalPath) ? logo.LocalPath : null;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<InstalledApp> ListWin32()
    {
        var apps = new List<InstalledApp>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Per-machine 64-bit, per-machine 32-bit, then per-user.
        ReadUninstallKey(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", apps, seen);
        ReadUninstallKey(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", apps, seen);
        ReadUninstallKey(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", apps, seen);

        // Legacy programs with no Uninstall entry of their own (Remote Desktop Connection, the Windows 10
        // Snipping Tool, brlapi) still get a row whenever they are on this PC.
        foreach (var legacyApp in LegacyApps.All)
        {
            if (!seen.Contains($"legacy:{legacyApp.Id}") && WindowsLegacyAppService.Detect(legacyApp))
            {
                apps.Add(new InstalledApp(
                    Id: $"legacy:{legacyApp.Id}",
                    Name: legacyApp.Name,
                    Publisher: "Microsoft Corporation",
                    Version: string.Empty,
                    Kind: InstalledAppKind.Win32,
                    Legacy: legacyApp));
            }
        }

        return apps.OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase);
    }

    private static void ReadUninstallKey(RegistryHive hive, string path, List<InstalledApp> apps, HashSet<string> seen)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(path);
            if (key is null)
            {
                return;
            }

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                try
                {
                    using var sub = key.OpenSubKey(subKeyName);
                    if (sub is null)
                    {
                        continue;
                    }

                    var displayName = (sub.GetValue("DisplayName") as string)?.Trim();
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        continue;
                    }

                    // Legacy programs (Edge and WebView2 hide their removal) are offered anyway:
                    // AkariOS-Ultimate's script removes them, not the entry's own uninstaller.
                    var legacyApp = LegacyApps.ForUninstallName(displayName);

                    // Updates, components and entries with no removal must not be offered.
                    if (legacyApp is null && (IsTruthy(sub.GetValue("SystemComponent")) || IsTruthy(sub.GetValue("NoRemove"))))
                    {
                        continue;
                    }

                    if (sub.GetValue("ParentKeyName") is string parent && !string.IsNullOrWhiteSpace(parent))
                    {
                        continue;
                    }

                    var uninstall = (sub.GetValue("UninstallString") as string)?.Trim();
                    var quiet = (sub.GetValue("QuietUninstallString") as string)?.Trim();
                    if (legacyApp is null && string.IsNullOrWhiteSpace(uninstall) && string.IsNullOrWhiteSpace(quiet))
                    {
                        continue;
                    }

                    // One row per legacy program, however many entries it left (one per KB update, say).
                    if (!seen.Add(legacyApp is null ? displayName : $"legacy:{legacyApp.Id}"))
                    {
                        continue;
                    }

                    if (legacyApp is not null)
                    {
                        displayName = legacyApp.Name;
                    }

                    var publisher = (sub.GetValue("Publisher") as string)?.Trim() ?? string.Empty;
                    var version = (sub.GetValue("DisplayVersion") as string)?.Trim() ?? string.Empty;

                    apps.Add(new InstalledApp(
                        Id: $"win32:{hive}\\{subKeyName}",
                        Name: displayName,
                        Publisher: publisher,
                        Version: version,
                        Kind: InstalledAppKind.Win32,
                        UninstallString: uninstall,
                        QuietUninstallString: quiet,
                        IconPath: InstalledAppText.IconPath(sub.GetValue("DisplayIcon") as string),
                        Legacy: legacyApp));
                }
                catch
                {
                    // One bad uninstall key must never hide every other app.
                }
            }
        }
        catch
        {
            // A missing hive (e.g. WOW6432Node on some machines) is not an error.
        }
    }

    private static bool IsTruthy(object? value) => value switch
    {
        int i => i != 0,
        string s => s == "1",
        _ => false,
    };

    public async Task UninstallAsync(InstalledApp app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Kind == InstalledAppKind.Appx)
        {
            await UninstallAppxAsync(app, cancellationToken);
            return;
        }

        if (app.Legacy is { } script)
        {
            await legacy.RemoveAsync(script, cancellationToken);
            return;
        }

        await UninstallWin32Async(app, cancellationToken);
    }

    private static async Task UninstallAppxAsync(InstalledApp app, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(app.PackageFullName))
        {
            throw new InvalidOperationException($"\"{app.Name}\" has no package identity to remove.");
        }

        // The Store apps tab's removal, so a package removed from here stays removed as well.
        await WindowsStoreAppService.RemoveForEveryoneAsync(app.PackageFullName, app.PackageFamilyName, app.Name, cancellationToken);
    }

    private static async Task UninstallWin32Async(InstalledApp app, CancellationToken cancellationToken)
    {
        var command = !string.IsNullOrWhiteSpace(app.QuietUninstallString)
            ? app.QuietUninstallString
            : app.UninstallString;

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new InvalidOperationException($"\"{app.Name}\" has no uninstall command.");
        }

        // Quiet string first; otherwise make MSI removals silent ourselves.
        if (string.IsNullOrWhiteSpace(app.QuietUninstallString) && IsMsi(command))
        {
            command = MakeMsiQuiet(command);
        }

        SplitCommand(command, out var file, out var args);

        var start = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            UseShellExecute = true,
            Verb = "runas",
        };

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"Could not start the uninstaller for \"{app.Name}\".");

        await process.WaitForExitAsync(cancellationToken);

        // 0 is success; 3010 means success with a reboot needed (leave that to Windows).
        if (process.ExitCode != 0 && process.ExitCode != 3010)
        {
            throw new InvalidOperationException(
                $"The uninstaller for \"{app.Name}\" exited with code {process.ExitCode}.");
        }
    }

    private static bool IsMsi(string command) =>
        command.Contains("msiexec", StringComparison.OrdinalIgnoreCase);

    private static string MakeMsiQuiet(string command)
    {
        // msiexec /I{...} -> msiexec /X{...} /qn /norestart; an uninstall /X just gains the flags.
        var quiet = command.Replace("/I", "/X", StringComparison.OrdinalIgnoreCase);
        if (!quiet.Contains("/qn", StringComparison.OrdinalIgnoreCase))
        {
            quiet += " /qn";
        }

        if (!quiet.Contains("norestart", StringComparison.OrdinalIgnoreCase))
        {
            quiet += " /norestart";
        }

        return quiet;
    }

    private static void SplitCommand(string command, out string file, out string args)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end > 1)
            {
                file = command[1..end];
                args = command[(end + 1)..].Trim();
                return;
            }
        }

        // Unquoted: the executable ends at the first ".exe" boundary when present.
        var exeIndex = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
        {
            file = command[..(exeIndex + 4)].Trim().Trim('"');
            args = command[(exeIndex + 4)..].Trim();
            return;
        }

        var space = command.IndexOf(' ');
        if (space < 0)
        {
            file = command.Trim('"');
            args = string.Empty;
            return;
        }

        file = command[..space].Trim('"');
        args = command[(space + 1)..].Trim();
    }
}
