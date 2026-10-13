using System.Diagnostics;
using AkariDash.Core.Apps;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace AkariDash.App.Services;

/// <summary>
/// Lists, removes and restores Store packages through <see cref="PackageManager"/>. Runs elevated (see
/// app.manifest), which listing every user's packages and removing for all of them needs.
/// </summary>
public sealed class WindowsStoreAppService : IStoreAppService
{
    private const string DeprovisionedRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Deprovisioned";

    public Task<IReadOnlyList<StoreApp>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<StoreApp>>(List, cancellationToken);

    private static List<StoreApp> List()
    {
        var apps = new Dictionary<string, StoreApp>(StringComparer.OrdinalIgnoreCase);
        // This account's packages. Every user's (FindPackages) also returns a package removed for all of
        // them until Windows has cleaned it off the disk, so a removal looked as if it had not happened.
        // Restoration still searches every user's, which is where a copy to register again is found.
        IEnumerable<Package> found = new PackageManager().FindPackagesForUser(string.Empty);

        foreach (var package in found)
        {
            try
            {
                if (package.IsResourcePackage || apps.ContainsKey(package.Id.FullName))
                {
                    continue;
                }

                var id = package.Id;
                var kind = package.IsFramework ? StoreAppKind.Framework
                    : package.SignatureKind == PackageSignatureKind.System ? StoreAppKind.System
                    : StoreAppKind.App;

                apps[id.FullName] = new StoreApp(
                    Name: id.Name,
                    DisplayName: InstalledAppText.PackageName(SafeDisplayName(package), id.Name),
                    Publisher: string.IsNullOrWhiteSpace(package.PublisherDisplayName) ? id.Publisher : package.PublisherDisplayName,
                    Version: $"{id.Version.Major}.{id.Version.Minor}.{id.Version.Build}.{id.Version.Revision}",
                    Architecture: id.Architecture.ToString().ToLowerInvariant(),
                    FullName: id.FullName,
                    FamilyName: id.FamilyName,
                    Kind: kind,
                    IconPath: LogoPath(package));
            }
            catch
            {
                // One unreadable package must never hide every other one.
            }
        }

        return [.. apps.Values.OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task RemoveAsync(StoreApp app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (AppxKeepList.ProtectedBecause(app.Name) is { } reason)
        {
            throw new InvalidOperationException($"\"{app.DisplayName}\" is never removed: {reason}.");
        }

        await RemoveForEveryoneAsync(app.FullName, app.FamilyName, app.DisplayName, cancellationToken);
    }

    /// <summary>
    /// AkariOS-Ultimate's removal (Remove-AppxPackage -AllUsers), then the two steps that keep the app
    /// from coming back. Shared with the Uninstaller tab, so a Store app removed there stays gone too.
    /// </summary>
    internal static async Task RemoveForEveryoneAsync(string fullName, string? familyName, string displayName, CancellationToken cancellationToken)
    {
        var manager = new PackageManager();
        Exception? firstFailure = null;
        var removed = false;

        // Prefer removing for every user (we run elevated); fall back to the current user.
        foreach (var options in new[] { RemovalOptions.RemoveForAllUsers, RemovalOptions.None })
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await manager.RemovePackageAsync(fullName, options).AsTask(cancellationToken);
                removed = true;
                break;
            }
            catch (Exception ex)
            {
                firstFailure ??= ex;
            }
        }

        if (!removed)
        {
            throw new InvalidOperationException($"Could not uninstall \"{displayName}\": {firstFailure?.Message}", firstFailure);
        }

        if (string.IsNullOrWhiteSpace(familyName))
        {
            return;
        }

        try
        {
            // The key feature updates read before putting an inbox app back.
            using (Registry.LocalMachine.CreateSubKey(AppxDeprovisioning.DeprovisionedKey(familyName)))
            {
            }

            // New accounts get provisioned packages; an app the user installed from the Store never was
            // one, so a failure here only matters while it is still provisioned.
            try
            {
                await manager.DeprovisionPackageForAllUsersAsync(familyName).AsTask(cancellationToken);
            }
            catch (Exception) when (!IsProvisioned(manager, familyName))
            {
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"\"{displayName}\" was uninstalled, but Windows may reinstall it: {ex.Message}", ex);
        }
    }

    public async Task RestoreAsync(RestorableApp app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Undo the keep-removed key first, whatever its publisher suffix, or Windows refuses it again.
        using (var deprovisioned = Registry.LocalMachine.OpenSubKey(DeprovisionedRoot, writable: true))
        {
            foreach (var family in deprovisioned?.GetSubKeyNames() ?? [])
            {
                if (family.StartsWith(app.PackageName + "_", StringComparison.OrdinalIgnoreCase))
                {
                    deprovisioned!.DeleteSubKeyTree(family, throwOnMissingSubKey: false);
                }
            }
        }

        if (await TryRegisterFromDiskAsync(app.PackageName, cancellationToken))
        {
            return;
        }

        if (app.StoreId is null)
        {
            throw new InvalidOperationException($"\"{app.Name}\" is no longer on this PC and the Microsoft Store no longer offers it.");
        }

        await InstallFromStoreAsync(app, cancellationToken);
    }

    // AkariOS-Ultimate's Install all UWP apps: register the package again from the files Windows kept,
    // which works while another user still has it or Windows has not cleaned it up yet. Not in
    // development mode, as -DisableDevelopmentMode there says.
    private static async Task<bool> TryRegisterFromDiskAsync(string packageName, CancellationToken cancellationToken)
    {
        var manager = new PackageManager();
        IEnumerable<Package> candidates;
        try
        {
            candidates = manager.FindPackages()
                .Where(package => string.Equals(package.Id.Name, packageName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch
        {
            return false;
        }

        foreach (var package in candidates)
        {
            try
            {
                var manifest = Path.Combine(package.InstalledLocation.Path, "AppxManifest.xml");
                if (!File.Exists(manifest))
                {
                    continue;
                }

                await manager.RegisterPackageAsync(new Uri(manifest), null, DeploymentOptions.ForceApplicationShutdown)
                    .AsTask(cancellationToken);
                return true;
            }
            catch
            {
                // Try the next copy, then the Store.
            }
        }

        return false;
    }

    private static async Task InstallFromStoreAsync(RestorableApp app, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("winget")
        {
            ArgumentList =
            {
                "install", "--id", app.StoreId!, "--source", "msstore", "--exact", "--silent",
                "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity",
            },
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not reinstall \"{app.Name}\": winget is not available ({ex.Message}). Restore Microsoft Store and App Installer first.", ex);
        }

        using (process)
        {
            var output = await process!.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                var last = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
                throw new InvalidOperationException(
                    $"Could not reinstall \"{app.Name}\" from the Microsoft Store (winget exit code 0x{process.ExitCode:X8}){(last is null ? "." : $": {last}")}");
            }
        }
    }

    private static bool IsProvisioned(PackageManager manager, string familyName)
    {
        try
        {
            return manager.FindProvisionedPackages()
                .Any(package => string.Equals(package.Id.FamilyName, familyName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static string? SafeDisplayName(Package package)
    {
        try
        {
            return package.DisplayName;
        }
        catch
        {
            return null;
        }
    }

    private static string? LogoPath(Package package)
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
}
