using System.Diagnostics;
using System.Text;
using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AkariDash.App.Services;

/// <summary>
/// Does the work behind AkariOS-Ultimate's Graphics entries: finds, downloads, unpacks (with 7-Zip,
/// as AkariOS does), installs and configures a graphics driver, cleans it with DDU through the
/// safe-mode companion, and runs the settings, DirectX and C++ entries. Everything that decides
/// anything is data in Core; this class only does the network, process and registry work. In the
/// Phase 1 build every write refuses.
/// </summary>
public sealed class WindowsDriverService(ILogger<WindowsDriverService> logger) : IDriverService
{
    private const string DisplayClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
    private const string DisplayClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";
    private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";
    private const string RunOnceKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string MsiKey = @"Device Parameters\Interrupt Management\MessageSignaledInterruptProperties";

    // AMD answers the default .NET client with a refusal, so this looks like a browser (Graphics.ps1:228-232).
    private static readonly HttpClient Http = CreateClient();

    public bool IsDryRun => BuildInfo.IsDryRunOnly;

    private static string WorkFolder(GpuVendor? vendor) =>
        Path.Combine(Path.GetTempPath(), "Akari-Dash", "drivers", vendor?.ToString().ToLowerInvariant() ?? "tools");

    private static string ResetFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Akari-Dash", "driver-reset");

    private static string DduFolder => Path.Combine(ResetFolder, "ddu");

    private static string StagedFolder(GpuVendor vendor) =>
        Path.Combine(ResetFolder, "staged", vendor.ToString().ToLowerInvariant());

    public async Task<AvailableDriver?> LatestAsync(GpuVendor vendor, CancellationToken cancellationToken = default)
    {
        if (vendor == GpuVendor.Intel)
        {
            return null;
        }

        try
        {
            var body = await Http.GetStringAsync(DriverLookup.Endpoint(vendor), cancellationToken);
            var driver = DriverLookup.Parse(vendor, body);
            if (driver is null)
            {
                logger.LogWarning("{Vendor}'s driver source answered, but not with a driver we recognise", vendor);
            }

            return driver;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Could not ask {Vendor} for its current driver", vendor);
            return null;
        }
    }

    public Task<InstalledDriver?> InstalledAsync(GpuVendor vendor, CancellationToken cancellationToken = default)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            foreach (var adapter in Adapters(baseKey, vendor, writable: false))
            {
                using (adapter)
                {
                    var name = adapter.GetValue("DriverDesc") as string ?? vendor.DisplayName();
                    var version = DriverVersion.FromWindows(
                        vendor,
                        adapter.GetValue("DriverVersion") as string,
                        adapter.GetValue("RadeonSoftwareVersion") as string);
                    return Task.FromResult<InstalledDriver?>(new InstalledDriver(name, version));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the installed {Vendor} driver", vendor);
        }

        return Task.FromResult<InstalledDriver?>(null);
    }

    public string Describe(IReadOnlyList<DriverExtra> steps) =>
        string.Join(Environment.NewLine, steps.Select(step => step.Describe()));

    public string Describe(DriverRow row, string? installerPath = null)
    {
        var vendor = row.Vendor.DisplayName();
        var text = new StringBuilder();
        switch (row.Kind)
        {
            case DriverStepKind.Clean or DriverStepKind.CleanManual:
                text.AppendLine("Download the latest driver for each card in the PC, so it is there after the clean");
                text.AppendLine(new EnsureSevenZip().Describe());
                text.AppendLine($"Download DDU from {DriverClean.DduDownload} and unpack it with 7-Zip to {DduFolder}");
                text.AppendLine($"Write DDU's settings to {DriverClean.DduSettingsFile}, read-only");
                text.AppendLine($@"Set HKLM\SOFTWARE\{DriverClean.SearchOrderConfig.KeyPath}\SearchOrderConfig = 0");
                text.AppendLine($@"Set HKLM\{RunOnceKey}\{DriverClean.RunOnceValueName} = {DriverClean.RunOnceCommand(Path.Combine(AppContext.BaseDirectory, DriverClean.CompanionExecutable), DriverResetState.DefaultPath)}");
                text.AppendLine("Run bcdedit /set {current} safeboot minimal, then restart");
                text.AppendLine(row.Kind == DriverStepKind.Clean
                    ? "In safe mode: bcdedit /deletevalue {current} safeboot, then " + DriverInstallCommand.Describe(DriverClean.DduExecutable, DriverClean.Arguments) + ", then restart"
                    : "In safe mode: bcdedit /deletevalue {current} safeboot, then open DDU, then restart once it closes");
                return text.ToString().TrimEnd();

            case DriverStepKind.Install:
                text.AppendLine(row.Vendor == GpuVendor.Intel
                    ? $"Open {DriverLookup.Endpoint(row.Vendor)}"
                    : $"Download the latest {vendor} driver installer and open it");
                return text.ToString().TrimEnd();

            case DriverStepKind.Debloat:
                var plan = installerPath is null ? null : DriverDebloat.Plan(row.Vendor, Path.GetFileName(installerPath));
                if (plan is null)
                {
                    return $"Refuse: {Path.GetFileName(installerPath ?? "(no file)")} is not a {vendor} driver installer.";
                }

                text.AppendLine(new EnsureSevenZip().Describe());
                text.AppendLine($"Unpack {installerPath} with 7-Zip");
                if (plan.RemoveAfterExtracting.Count > 0)
                {
                    text.AppendLine($"Remove {plan.RemoveAfterExtracting.Count} extras: {string.Join(", ", plan.RemoveAfterExtracting)}");
                }

                if (plan.TurnOffInConfig.Count > 0)
                {
                    text.AppendLine($"Switch off the components in: {string.Join(", ", plan.TurnOffInConfig)}");
                }

                text.AppendLine("Run " + DriverInstallCommand.Describe(plan.Setup, plan.SetupArguments));
                if (plan.AfterSetup is { } after)
                {
                    text.AppendLine("Run " + DriverInstallCommand.Describe(after, plan.AfterSetupArguments ?? []));
                }

                break;

            default:
                text.AppendLine(row.Vendor switch
                {
                    GpuVendor.Nvidia => "Install 7-Zip, download the latest NVIDIA driver, unpack it and run "
                        + DriverInstallCommand.Describe("setup.exe", DriverInstallCommand.Install(row, "setup.exe")),
                    GpuVendor.Amd => "Download AMD's installer, open it and wait for it to finish",
                    _ => installerPath is null
                        ? $"Open {DriverLookup.Endpoint(row.Vendor)} (no Intel installer in {DriverInbox.Folder})"
                        : $"Open {installerPath} and wait for it to finish",
                });
                break;
        }

        foreach (var extra in DriverExtras.For(row.Vendor, row.Kind))
        {
            if (extra is not ApplyRegistrySettings)
            {
                text.AppendLine(extra.Describe());
                continue;
            }

            foreach (var setting in DriverSettings.For(row.Vendor))
            {
                text.AppendLine($"Set {Where(setting)}\\{setting.Name} = {setting.Value} ({setting.Kind})");
            }
        }

        return text.ToString().TrimEnd();
    }

    public string? DroppedInstaller(GpuVendor vendor) =>
        Directory.Exists(DriverInbox.Folder)
            ? DriverInbox.Installers(Directory.EnumerateFiles(DriverInbox.Folder))
                .FirstOrDefault(file => DriverDebloat.Plan(vendor, Path.GetFileName(file)) is not null)
            : null;

    public async Task<DriverStepOutcome> InstallAsync(
        DriverRow row,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        RefuseDryRun(row.Title, Describe(row));
        var vendor = row.Vendor;

        if (row.Kind == DriverStepKind.Install)
        {
            return await OpenInstallerAsync(vendor, progress, cancellationToken);
        }

        if (vendor == GpuVendor.Amd)
        {
            return await RunAmdInstallerAsync(row, progress, cancellationToken);
        }

        if (vendor == GpuVendor.Intel)
        {
            return await RunIntelInstallerAsync(row, progress, cancellationToken);
        }

        // NVIDIA, as AkariOS: 7-Zip, the latest driver unpacked, setup -s -noreboot -noeula -clean.
        await EnsureSevenZipAsync(progress, cancellationToken);
        progress?.Report("Finding the latest NVIDIA driver…");
        var latest = await LatestAsync(vendor, cancellationToken);
        var staged = Staged(vendor, latest);
        if (latest is null && staged is null)
        {
            throw new InvalidOperationException(
                "Could not find the latest NVIDIA driver: NVIDIA's driver source did not answer with one. Nothing was changed.");
        }

        var folder = FreshFolder(vendor);
        try
        {
            var installer = staged ?? await DownloadAsync(latest!.DownloadUrl, folder, progress, cancellationToken);
            var unpacked = Path.Combine(folder, "unpacked");
            progress?.Report($"Unpacking {Path.GetFileName(installer)}…");
            await UnpackAsync(installer, unpacked, cancellationToken);

            progress?.Report($"Installing {Path.GetFileName(installer)}…");
            var outcome = await RunSetupAsync(
                row, Path.Combine(unpacked, "setup.exe"), DriverInstallCommand.Install(row, "setup.exe"), cancellationToken);
            TryDelete(StagedFolder(vendor));

            return await ConfigureAsync(row, outcome, progress, cancellationToken);
        }
        finally
        {
            TryDelete(folder);
        }
    }

    public async Task<DriverStepOutcome> DebloatAsync(
        DriverRow row,
        string installerPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Checked before anything else, the Dry Run included: another vendor's file is never touched.
        var plan = DriverDebloat.Plan(row.Vendor, Path.GetFileName(installerPath))
            ?? throw new InvalidOperationException(
                $"{Path.GetFileName(installerPath)} is not a {row.Vendor.DisplayName()} driver installer, so nothing was done with it.");

        RefuseDryRun(row.Title, Describe(row, installerPath));
        await EnsureSevenZipAsync(progress, cancellationToken);

        var folder = FreshFolder(row.Vendor);
        try
        {
            var unpacked = Path.Combine(folder, "unpacked");
            progress?.Report($"Unpacking {Path.GetFileName(installerPath)}…");
            await UnpackAsync(installerPath, unpacked, cancellationToken);

            progress?.Report("Removing the extras…");
            foreach (var entry in plan.RemoveAfterExtracting)
            {
                RemoveIfThere(Inside(unpacked, entry));
                logger.LogInformation("Debloat: removed {Entry}", entry);
            }

            foreach (var entry in plan.TurnOffInConfig)
            {
                var path = Inside(unpacked, entry);
                if (File.Exists(path))
                {
                    var content = await File.ReadAllTextAsync(path, cancellationToken);
                    await File.WriteAllTextAsync(path, DriverDebloat.TurnOff(entry, content), cancellationToken);
                    logger.LogInformation("Debloat: switched off the components in {Entry}", entry);
                }
            }

            progress?.Report($"Installing the {row.Vendor.DisplayName()} driver…");
            var outcome = await RunSetupAsync(row, Inside(unpacked, plan.Setup), plan.SetupArguments, cancellationToken);

            // Intel's control app ships beside the driver and installs on its own (Graphics.ps1:720-721).
            if (plan.AfterSetup is { } afterSetup)
            {
                var extras = Inside(unpacked, Path.GetDirectoryName(afterSetup)!);
                var program = Directory.Exists(extras)
                    ? Directory.EnumerateFiles(extras, Path.GetFileName(afterSetup)).FirstOrDefault()
                    : null;
                if (program is not null)
                {
                    progress?.Report($"Installing {Path.GetFileName(program)}…");
                    var run = await ProcessRunner.RunAsync(program, plan.AfterSetupArguments ?? [], cancellationToken);
                    logger.LogInformation("{Program} exited with {ExitCode}", program, run?.ExitCode);
                }
                else
                {
                    logger.LogWarning("The package has no {Pattern}", afterSetup);
                }
            }

            return await ConfigureAsync(row, outcome, progress, cancellationToken, usedInstaller: installerPath);
        }
        finally
        {
            TryDelete(folder);
        }
    }

    /// <summary>
    /// AkariOS's driver clean, with a way back staged first: the latest driver for each card is put
    /// on disk before anything is removed, so if it cannot be fetched the run stops here with the old
    /// driver still in place. Then DDU, its settings, Windows Update's driver search off, RunOnce,
    /// safe mode, and the restart.
    /// </summary>
    public async Task<DriverStepOutcome> CleanAsync(
        bool manual,
        IReadOnlySet<GpuVendor> cards,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var title = manual ? "DDU: Manual" : "DDU: Auto";
        RefuseDryRun(title, Describe(new DriverRow("ddu", GpuVendor.Nvidia, manual ? DriverStepKind.CleanManual : DriverStepKind.Clean, title, title, DriverRisk.Advanced)));

        var companion = Path.Combine(AppContext.BaseDirectory, DriverClean.CompanionExecutable);
        if (!File.Exists(companion))
        {
            throw new InvalidOperationException($"{DriverClean.CompanionExecutable} is missing from the app's folder, so nothing was changed. Reinstall Akari Dash.");
        }

        foreach (var card in cards.Where(card => card != GpuVendor.Intel))
        {
            await StageAsync(card, progress, cancellationToken);
        }

        await EnsureSevenZipAsync(progress, cancellationToken);

        progress?.Report("Downloading DDU…");
        if (Directory.Exists(DduFolder))
        {
            RemoveIfThere(DduFolder);
        }

        Directory.CreateDirectory(ResetFolder);
        var download = await DownloadAsync(DriverClean.DduDownload, ResetFolder, progress, cancellationToken);
        await UnpackAsync(download, DduFolder, cancellationToken);
        TryDelete(download);

        var program = Directory.EnumerateFiles(DduFolder, DriverClean.DduExecutable, SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new InvalidOperationException($"DDU did not unpack to {DduFolder} (no {DriverClean.DduExecutable}), so nothing was changed.");
        var ddu = Path.GetDirectoryName(program)!;

        // Read-only, as AkariOS leaves it, so DDU cannot write its own choices back over these.
        var settings = Path.Combine(ddu, DriverClean.DduSettingsFile);
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        if (File.Exists(settings))
        {
            File.SetAttributes(settings, FileAttributes.Normal);
        }

        await File.WriteAllTextAsync(settings, DriverClean.DduSettingsXml, cancellationToken);
        File.SetAttributes(settings, FileAttributes.ReadOnly);

        Write(null, DriverClean.SearchOrderConfig);

        var state = new DriverResetState(manual, ddu, DateTimeOffset.Now, DriverResetStage.Scheduled);
        await File.WriteAllTextAsync(DriverResetState.DefaultPath, state.ToJson(), cancellationToken);

        progress?.Report("Setting the next start to safe mode…");
        using (var runOnce = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).CreateSubKey(RunOnceKey, writable: true))
        {
            runOnce.SetValue(DriverClean.RunOnceValueName, DriverClean.RunOnceCommand(companion, DriverResetState.DefaultPath), RegistryValueKind.String);
        }

        var bcd = await ProcessRunner.RunAsync("bcdedit.exe", ["/set", "{current}", "safeboot", "minimal"], cancellationToken);
        if (bcd?.ExitCode != 0)
        {
            // Without safe mode the companion would run DDU in normal Windows; take it all back.
            RemoveRunOnce();
            TryDelete(DriverResetState.DefaultPath);
            throw new InvalidOperationException(
                $"Could not set the next start to safe mode (bcdedit {(bcd is null ? "did not start" : $"exit code {bcd.ExitCode}")}), so nothing was changed.");
        }

        logger.LogInformation("DDU clean scheduled ({Mode}); DDU in {Folder}; restarting", manual ? "manual" : "auto", ddu);
        progress?.Report("Restarting…");
        Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 5") { UseShellExecute = false, CreateNoWindow = true })?.Dispose();
        return DriverStepOutcome.CleanScheduled;
    }

    public async Task<bool> RunStepsAsync(
        IReadOnlyList<DriverExtra> steps,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        RefuseDryRun("This entry", Describe(steps));

        var allApplied = true;
        foreach (var step in steps)
        {
            progress?.Report(step.Describe() + "…");
            try
            {
                if (!await RunExtraAsync(null, step, null, cancellationToken))
                {
                    allApplied = false;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                allApplied = false;
                logger.LogError(ex, "Could not {Step}", step.Describe());
            }
        }

        return allApplied;
    }

    public DriverResetState? ReadReset()
    {
        try
        {
            return File.Exists(DriverResetState.DefaultPath)
                ? DriverResetState.FromJson(File.ReadAllText(DriverResetState.DefaultPath))
                : null;
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not read {Path}", DriverResetState.DefaultPath);
            return null;
        }
    }

    public void ForgetReset()
    {
        if (IsDryRun)
        {
            return;
        }

        TryDelete(DriverResetState.DefaultPath);
        try
        {
            RemoveIfThere(DduFolder);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not tidy away {Path}", DduFolder);
        }
    }

    private void RefuseDryRun(string title, string plan)
    {
        if (!IsDryRun)
        {
            return;
        }

        // Phase 1 is safe to launch on any PC because it does nothing. Say what a real run would
        // have done, then refuse rather than quietly claim a success that did not happen.
        logger.LogInformation("Dry Run only: {Title} would have done: {Plan}", title, plan);
        throw new InvalidOperationException($"This is the Dry Run build, so \"{title}\" did nothing.");
    }

    /// <summary>
    /// Driver install (updated), as AkariOS: NVIDIA's and AMD's installer downloaded and opened for
    /// the person to run, Intel's download page opened.
    /// </summary>
    private async Task<DriverStepOutcome> OpenInstallerAsync(GpuVendor vendor, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var latest = await LatestAsync(vendor, cancellationToken);
        var staged = vendor == GpuVendor.Intel ? null : Staged(vendor, latest);
        if (latest is null && staged is null)
        {
            if (vendor != GpuVendor.Intel)
            {
                throw new InvalidOperationException(
                    $"Could not find the latest {vendor.DisplayName()} driver: {vendor.DisplayName()}'s driver source did not answer with one.");
            }

            OpenTarget(DriverLookup.Endpoint(vendor).AbsoluteUri);
            return DriverStepOutcome.HandedOffToInstaller;
        }

        // Kept after the run: the installer is still open when this returns.
        var installer = staged ?? await DownloadAsync(latest!.DownloadUrl, FreshFolder(vendor), progress, cancellationToken);
        logger.LogInformation("Opening {Installer}", installer);
        Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true })?.Dispose();
        return DriverStepOutcome.HandedOffToInstaller;
    }

    /// <summary>AMD's install and settings (Graphics.ps1:355-412): its installer, waited for, then AkariOS's steps.</summary>
    private async Task<DriverStepOutcome> RunAmdInstallerAsync(DriverRow row, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var latest = await LatestAsync(GpuVendor.Amd, cancellationToken);
        var staged = Staged(GpuVendor.Amd, latest);
        if (latest is null && staged is null)
        {
            throw new InvalidOperationException("Could not find the latest AMD driver: AMD's driver page did not answer with one. Nothing was changed.");
        }

        var folder = FreshFolder(GpuVendor.Amd);
        try
        {
            var installer = staged ?? await DownloadAsync(latest!.DownloadUrl, folder, progress, cancellationToken);
            await OpenAndWaitAsync(installer, progress, cancellationToken);
            TryDelete(StagedFolder(GpuVendor.Amd));

            var outcome = await ConfigureAsync(row, DriverStepOutcome.HandedOffToInstaller, progress, cancellationToken);
            return outcome == DriverStepOutcome.InstalledWithoutSettings ? outcome : DriverStepOutcome.HandedOffToInstaller;
        }
        finally
        {
            TryDelete(folder);
        }
    }

    /// <summary>
    /// Intel's install and settings (Graphics.ps1:415-447): AkariOS opens Intel's page and asks for
    /// the installer the person downloaded. Here it is the one in the drop folder; without one, the
    /// page opens so it can be downloaded there.
    /// </summary>
    private async Task<DriverStepOutcome> RunIntelInstallerAsync(DriverRow row, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (DroppedInstaller(GpuVendor.Intel) is not { } dropped)
        {
            OpenTarget(DriverLookup.Endpoint(GpuVendor.Intel).AbsoluteUri);
            throw new InvalidOperationException(
                $"Intel's download page is open. Save the driver (gfx_win_....exe) to {DriverInbox.Folder}, then press Install again.");
        }

        await OpenAndWaitAsync(dropped, progress, cancellationToken);
        var outcome = await ConfigureAsync(row, DriverStepOutcome.HandedOffToInstaller, progress, cancellationToken, usedInstaller: dropped);
        return outcome == DriverStepOutcome.InstalledWithoutSettings ? outcome : DriverStepOutcome.HandedOffToInstaller;
    }

    private async Task OpenAndWaitAsync(string installer, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        progress?.Report($"Waiting for {Path.GetFileName(installer)} to finish…");
        logger.LogInformation("Opening {Installer} and waiting for it", installer);
        using var process = Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true });
        if (process is not null)
        {
            await process.WaitForExitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Does everything AkariOS does after the driver (<see cref="DriverExtras"/>), in its order. A
    /// step that fails does not undo the install, nor stop the steps after it; the row says not every
    /// step applied instead. Clean-up of something that is not there is not a failure.
    /// </summary>
    private async Task<DriverStepOutcome> ConfigureAsync(
        DriverRow row,
        DriverStepOutcome installed,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        string? usedInstaller = null)
    {
        var allApplied = true;
        foreach (var extra in DriverExtras.For(row.Vendor, row.Kind))
        {
            progress?.Report(extra.Describe() + "…");
            try
            {
                if (!await RunExtraAsync(row.Vendor, extra, usedInstaller, cancellationToken))
                {
                    allApplied = false;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                allApplied = false;
                logger.LogError(ex, "Could not {Step}", extra.Describe());
            }
        }

        return allApplied ? installed : DriverStepOutcome.InstalledWithoutSettings;
    }

    private async Task<bool> RunExtraAsync(GpuVendor? vendor, DriverExtra extra, string? usedInstaller, CancellationToken cancellationToken)
    {
        logger.LogInformation("{Step}", extra.Describe());
        switch (extra)
        {
            case ApplyRegistrySettings:
                var applied = true;
                foreach (var setting in vendor is { } card ? DriverSettings.For(card) : [])
                {
                    try
                    {
                        Write(vendor, setting);
                        logger.LogInformation("Set {Key}\\{Name} = {Value}", Where(setting), setting.Name, setting.Value);
                    }
                    catch (Exception ex)
                    {
                        applied = false;
                        logger.LogError(ex, "Could not set {Key}\\{Name}", Where(setting), setting.Name);
                    }
                }

                return applied;

            case SetRegistry set:
                Write(set.Vendor, set.Setting);
                return true;

            case DeleteRegistry delete:
                Delete(delete);
                return true;

            case SetMsiMode msi:
                return SetMsi(msi.Value);

            case OpenTarget open:
                OpenTarget(Expand(open.Target));
                return true;

            case EnsureSevenZip:
                await EnsureSevenZipAsync(null, cancellationToken);
                return true;

            case InstallRedistributable redist:
                return await InstallRedistributableAsync(redist, cancellationToken);

            case InstallDirectX directx:
                return await InstallDirectXAsync(directx, cancellationToken);

            case DownloadTool tool:
            {
                var folder = FreshFolder(vendor);
                try
                {
                    var downloaded = await DownloadAsync(tool.Source, folder, null, cancellationToken);
                    var destination = Expand(tool.Destination);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Move(downloaded, destination, overwrite: true);
                    return true;
                }
                finally
                {
                    TryDelete(folder);
                }
            }

            case InstallAppx appx:
            {
                var folder = FreshFolder(vendor);
                try
                {
                    var package = await DownloadAsync(appx.Source, folder, null, cancellationToken);
                    var result = await new Windows.Management.Deployment.PackageManager().AddPackageAsync(
                        new Uri(package), null, Windows.Management.Deployment.DeploymentOptions.ForceApplicationShutdown);
                    if (result.ExtendedErrorCode is { } error)
                    {
                        logger.LogError(error, "{Name} did not install: {Text}", appx.Name, result.ErrorText);
                        return false;
                    }

                    return true;
                }
                finally
                {
                    TryDelete(folder);
                }
            }

            case CreateShortcut shortcut:
            {
                var folder = shortcut.StartMenu
                    ? Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
                    : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var target = shortcut.Target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
                    ? shortcut.Target
                    : Expand(shortcut.Target);
                if (!target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) && !File.Exists(target))
                {
                    logger.LogWarning("{Target} is not there, so its shortcut was not made", target);
                    return false;
                }

                ShellShortcut.Create(Path.Combine(folder, shortcut.Name + ".lnk"), target, Expand(shortcut.WorkingDirectory));
                return true;
            }

            case MoveFile move:
            {
                var from = Expand(move.From);
                if (File.Exists(from))
                {
                    File.Move(from, Path.Combine(Expand(move.ToFolder), Path.GetFileName(from)), overwrite: true);
                }

                return true;
            }

            case RemovePath remove:
                RemoveIfThere(Expand(remove.Path));
                return true;

            case RemoveService service:
                await ProcessRunner.RunAsync("sc.exe", ["stop", service.Name], cancellationToken);
                var deleted = await ProcessRunner.RunAsync("sc.exe", ["delete", service.Name], cancellationToken);
                logger.LogInformation("sc delete {Service}: exit code {ExitCode}", service.Name, deleted?.ExitCode);
                return true;

            case RemoveRegistryValue value:
            {
                using var root = RegistryKey.OpenBaseKey(value.LocalMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64);
                using var key = root.OpenSubKey(value.KeyPath, writable: true);
                key?.DeleteValue(value.Name, throwOnMissingValue: false);
                return true;
            }

            case ClearUserKey clear:
            {
                using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                root.DeleteSubKeyTree(clear.KeyPath, throwOnMissingSubKey: false);
                root.CreateSubKey(clear.KeyPath)?.Dispose();
                return true;
            }

            case RemoveScheduledTask task:
                await ProcessRunner.RunAsync("schtasks.exe", ["/Delete", "/TN", task.Name, "/F"], cancellationToken);
                return true;

            case UninstallProduct product:
                foreach (var code in ProductCodes(product.DisplayNameContains))
                {
                    var run = await ProcessRunner.RunAsync("msiexec.exe", ["/x", code, "/qn", "/norestart"], cancellationToken);
                    logger.LogInformation("msiexec /x {Code}: exit code {ExitCode}", code, run?.ExitCode);
                }

                return true;

            case StopProcess stop:
                await StopAsync(stop.Name);
                return true;

            case OpenThenClose open:
            {
                var program = Expand(open.Program);
                if (!File.Exists(program))
                {
                    logger.LogWarning("{Program} is not there to open", program);
                    return true;
                }

                Process.Start(new ProcessStartInfo(program) { UseShellExecute = true })?.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(open.Seconds), cancellationToken);
                await StopAsync(open.ProcessName);
                return true;
            }

            case UnblockFiles unblock:
            {
                var folder = Expand(unblock.Folder);
                if (Directory.Exists(folder))
                {
                    foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                    {
                        // The mark is an alternate data stream on the file; deleting it unblocks the file.
                        try
                        {
                            File.Delete(file + ":Zone.Identifier");
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            logger.LogWarning(ex, "Could not unblock {File}", file);
                        }
                    }
                }

                return true;
            }

            case Wait wait:
                await Task.Delay(TimeSpan.FromSeconds(wait.Seconds), cancellationToken);
                return true;

            case ImportNvidiaProfile import:
                return await ImportNvidiaProfileAsync(Expand(import.Inspector), import.Reset, cancellationToken);

            case RemoveUsedInstaller:
                if (usedInstaller is not null)
                {
                    RemoveIfThere(usedInstaller);
                }

                return true;

            default:
                logger.LogError("No way to run {Step}", extra.GetType().Name);
                return false;
        }
    }

    /// <summary>Installs 7-Zip from the mirror when it is not already there, as AkariOS does (Graphics.ps1:8-20).</summary>
    private async Task EnsureSevenZipAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var program = Expand(SevenZip.Program);
        if (File.Exists(program))
        {
            return;
        }

        progress?.Report("Installing 7-Zip…");
        var folder = FreshFolder(null);
        try
        {
            var setup = await DownloadAsync(SevenZip.Download, folder, null, cancellationToken);
            var run = await ProcessRunner.RunAsync(setup, ["/S"], cancellationToken);
            logger.LogInformation("7-Zip setup exited with {ExitCode}", run?.ExitCode);
        }
        finally
        {
            TryDelete(folder);
        }

        if (!File.Exists(program))
        {
            throw new InvalidOperationException($"7-Zip did not install to {program}, so nothing was unpacked.");
        }

        foreach (var step in SevenZip.AfterInstall)
        {
            try
            {
                await RunExtraAsync(null, step, null, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not {Step}", step.Describe());
            }
        }
    }

    /// <summary>Unpacks an archive or self-extractor with 7-Zip, as AkariOS does (<c>7z x file -ofolder -y</c>).</summary>
    private async Task UnpackAsync(string archive, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        var run = await ProcessRunner.RunAsync(Expand(SevenZip.Program), SevenZip.Extract(archive, destination), cancellationToken)
            ?? throw new InvalidOperationException("7-Zip could not be started, so nothing was unpacked.");

        // 7-Zip: 0 is done, 1 is done with warnings; 2 and up mean it did not unpack.
        if (run.ExitCode > 1)
        {
            throw new InvalidOperationException(
                $"7-Zip could not unpack {Path.GetFileName(archive)} (exit code {run.ExitCode}){(run.LastLine is { } last ? $": {last}" : ".")}");
        }

        logger.LogInformation("Unpacked {Archive} to {Folder}", archive, destination);
    }

    private async Task<bool> InstallRedistributableAsync(InstallRedistributable redist, CancellationToken cancellationToken)
    {
        var folder = FreshFolder(null);
        try
        {
            var setup = await DownloadAsync(redist.Source, folder, null, cancellationToken);

            // The 2005 runtimes take a quoted msiexec line, so the arguments go as AkariOS writes them.
            using var process = Process.Start(new ProcessStartInfo(setup, redist.Arguments) { UseShellExecute = false, CreateNoWindow = true });
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);
            logger.LogInformation("{Name} exited with {ExitCode}", redist.Name, process.ExitCode);
            return true;
        }
        finally
        {
            TryDelete(folder);
        }
    }

    private async Task<bool> InstallDirectXAsync(InstallDirectX directx, CancellationToken cancellationToken)
    {
        var folder = FreshFolder(null);
        try
        {
            var download = await DownloadAsync(directx.Source, folder, null, cancellationToken);
            var unpacked = Path.Combine(folder, "directx");
            await UnpackAsync(download, unpacked, cancellationToken);

            var setup = Directory.EnumerateFiles(unpacked, "DXSETUP.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (setup is null)
            {
                logger.LogError("The DirectX package has no DXSETUP.exe");
                return false;
            }

            var run = await ProcessRunner.RunAsync(setup, ["/silent"], cancellationToken, Path.GetDirectoryName(setup));
            logger.LogInformation("DXSETUP exited with {ExitCode}", run?.ExitCode);
            return run?.ExitCode == 0;
        }
        finally
        {
            TryDelete(folder);
        }
    }

    /// <summary>
    /// MSISupported on every PCI display device (Graphics.ps1:1523-1528): AkariOS asks
    /// Get-PnpDevice -Class Display; the same devices are the ones whose Enum key carries the
    /// Display class.
    /// </summary>
    private bool SetMsi(int value)
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var pci = machine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\PCI");
        var any = false;
        foreach (var device in pci?.GetSubKeyNames() ?? [])
        {
            using var deviceKey = pci!.OpenSubKey(device);
            foreach (var instance in deviceKey?.GetSubKeyNames() ?? [])
            {
                using var instanceKey = deviceKey!.OpenSubKey(instance);
                if (!string.Equals(instanceKey?.GetValue("ClassGUID") as string, DisplayClassGuid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var msi = instanceKey!.CreateSubKey(MsiKey, writable: true);
                msi.SetValue("MSISupported", value, RegistryValueKind.DWord);
                logger.LogInformation(@"PCI\{Device}\{Instance}: MSISupported = {Value}", device, instance, value);
                any = true;
            }
        }

        return any;
    }

    private void OpenTarget(string target)
    {
        logger.LogInformation("Opening {Target}", target);

        // A shell: app id opens through Explorer; ms-settings: and a path through the shell itself.
        Process.Start(target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
            ? new ProcessStartInfo("explorer.exe", target) { UseShellExecute = true }
            : new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
    }

    private async Task<bool> ImportNvidiaProfileAsync(string inspector, bool reset, CancellationToken cancellationToken)
    {
        if (!File.Exists(inspector))
        {
            logger.LogWarning("Nvidia Profile Inspector is not at {Path}, so the NVIDIA profile was not imported", inspector);
            return false;
        }

        // The profile declares UTF-16, so it is written as UTF-16.
        var profile = Path.Combine(WorkFolder(GpuVendor.Nvidia), "inspector.nip");
        Directory.CreateDirectory(Path.GetDirectoryName(profile)!);
        await File.WriteAllTextAsync(profile, reset ? DriverSettings.NvidiaEmptyProfileXml : DriverSettings.NvidiaProfileXml, Encoding.Unicode, cancellationToken);

        try
        {
            var run = await ProcessRunner.RunAsync(inspector, ["-silentImport", "-silent", profile], cancellationToken);
            logger.LogInformation("Nvidia Profile Inspector exited with {ExitCode}", run?.ExitCode);
            return run is not null;
        }
        finally
        {
            TryDelete(profile);
        }
    }

    /// <summary>The driver staged for this vendor: the one matching the latest version, or any when the source cannot be asked.</summary>
    private static string? Staged(GpuVendor vendor, AvailableDriver? latest)
    {
        var folder = StagedFolder(vendor);
        if (!Directory.Exists(folder))
        {
            return null;
        }

        var files = Directory.EnumerateFiles(folder, "*.exe").ToList();
        return latest is null
            ? files.FirstOrDefault()
            : files.FirstOrDefault(file => Path.GetFileName(file).Contains(latest.Version, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Puts this card's latest driver on disk before a clean removes the old one.</summary>
    private async Task StageAsync(GpuVendor vendor, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        progress?.Report($"Finding the latest {vendor.DisplayName()} driver…");
        var latest = await LatestAsync(vendor, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Could not find the latest {vendor.DisplayName()} driver, so the old one was not removed. Nothing was changed.");

        if (Staged(vendor, latest) is { } already)
        {
            logger.LogInformation("Already staged: {Installer}", already);
            return;
        }

        TryDelete(StagedFolder(vendor));
        Directory.CreateDirectory(StagedFolder(vendor));
        await DownloadAsync(latest.DownloadUrl, StagedFolder(vendor), progress, cancellationToken);
    }

    private async Task<string> DownloadAsync(Uri url, string folder, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var path = Path.Combine(folder, Path.GetFileName(url.LocalPath));
        logger.LogInformation("Downloading {Url} to {Path}", url, path);

        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(path);

        var buffer = new byte[1 << 20];
        long received = 0;
        var lastPercent = -1;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;

            if (total is > 0)
            {
                var percent = (int)(received * 100 / total.Value);
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    progress?.Report($"Downloading {Path.GetFileName(path)} — {percent}%");
                }
            }
        }

        logger.LogInformation("Downloaded {Bytes} bytes", received);
        return path;
    }

    private async Task<DriverStepOutcome> RunSetupAsync(
        DriverRow row,
        string setup,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(setup))
        {
            throw new InvalidOperationException(
                $"The {row.Vendor.DisplayName()} package has no {Path.GetFileName(setup)} where it should be, so nothing was installed.");
        }

        logger.LogInformation("Running {CommandLine}", DriverInstallCommand.Describe(setup, arguments));
        var run = await ProcessRunner.RunAsync(setup, arguments, cancellationToken, Path.GetDirectoryName(setup))
            ?? throw new InvalidOperationException($"{Path.GetFileName(setup)} could not be started.");

        logger.LogInformation("{Setup} exited with {ExitCode}", setup, run.ExitCode);
        return run.ExitCode switch
        {
            0 => DriverStepOutcome.Completed,
            3010 or 1641 => DriverStepOutcome.CompletedNeedsRestart,
            _ => throw new InvalidOperationException(
                $"The {row.Vendor.DisplayName()} installer stopped with exit code {run.ExitCode}{(run.LastLine is { } last ? $": {last}" : ".")}"),
        };
    }

    private static void Write(GpuVendor? vendor, DriverSetting setting)
    {
        if (setting.Root == DriverSettingRoot.GraphicsClass)
        {
            // Only this vendor's adapters: a laptop's other card keeps its own settings.
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            var any = false;
            foreach (var adapter in Adapters(machine, vendor, writable: true))
            {
                using (adapter)
                {
                    using var key = setting.KeyPath.Length == 0 ? null : adapter.CreateSubKey(setting.KeyPath, writable: true);
                    SetValue(key ?? adapter, setting);
                    any = true;
                }
            }

            if (!any)
            {
                throw new InvalidOperationException($"There is no {vendor?.DisplayName() ?? "graphics"} adapter key to write {setting.Name} under.");
            }

            return;
        }

        using var root = OpenRoot(setting.Root);
        using var target = root.CreateSubKey(RootPath(setting.Root, setting.KeyPath), writable: true);
        SetValue(target, setting);
    }

    private static void Delete(DeleteRegistry delete)
    {
        if (delete.Root == DriverSettingRoot.GraphicsClass)
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            foreach (var adapter in Adapters(machine, delete.Vendor, writable: true))
            {
                using (adapter)
                {
                    DeleteBelow(adapter, delete.KeyPath, delete.Name);
                }
            }

            return;
        }

        using var root = OpenRoot(delete.Root);
        DeleteBelow(root, RootPath(delete.Root, delete.KeyPath), delete.Name);
    }

    private static void DeleteBelow(RegistryKey parent, string keyPath, string? name)
    {
        if (name is null)
        {
            parent.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
            return;
        }

        using var key = keyPath.Length == 0 ? null : parent.OpenSubKey(keyPath, writable: true);
        (keyPath.Length == 0 ? parent : key)?.DeleteValue(name, throwOnMissingValue: false);
    }

    private static RegistryKey OpenRoot(DriverSettingRoot root) => root == DriverSettingRoot.Hkcu
        ? RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64)
        : RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

    private static string RootPath(DriverSettingRoot root, string keyPath) => root switch
    {
        DriverSettingRoot.HklmServices => $@"{ServicesKey}\{keyPath}",
        DriverSettingRoot.HklmSoftware => $@"SOFTWARE\{keyPath}",
        _ => keyPath,
    };

    private static void SetValue(RegistryKey key, DriverSetting setting)
    {
        switch (setting.Kind)
        {
            case DriverSettingKind.Dword:
                key.SetValue(setting.Name, unchecked((int)uint.Parse(setting.Value)), RegistryValueKind.DWord);
                break;
            case DriverSettingKind.Binary:
                key.SetValue(setting.Name, Convert.FromHexString(setting.Value), RegistryValueKind.Binary);
                break;
            default:
                key.SetValue(setting.Name, setting.Value, RegistryValueKind.String);
                break;
        }
    }

    /// <summary>
    /// The numbered adapter keys under the Display class for <paramref name="vendor"/>, or for every
    /// NVIDIA, AMD and Intel card when it is <see langword="null"/>. The caller disposes each.
    /// </summary>
    private static IEnumerable<RegistryKey> Adapters(RegistryKey machine, GpuVendor? vendor, bool writable)
    {
        using var displayClass = machine.OpenSubKey(DisplayClassKey, writable: false);
        foreach (var name in displayClass?.GetSubKeyNames() ?? [])
        {
            if (!int.TryParse(name, out _))
            {
                continue;
            }

            var adapter = displayClass!.OpenSubKey(name, writable);
            if (adapter is null)
            {
                continue;
            }

            var found = GpuVendorExtensions.FromMatchingDeviceId(adapter.GetValue("MatchingDeviceId") as string);
            if (found is not null && (vendor is null || found == vendor))
            {
                yield return adapter;
            }
            else
            {
                adapter.Dispose();
            }
        }
    }

    private static string Where(DriverSetting setting) => setting.Root switch
    {
        DriverSettingRoot.Hkcu => $@"HKCU\{setting.KeyPath}",
        DriverSettingRoot.HklmServices => $@"HKLM\{ServicesKey}\{setting.KeyPath}",
        DriverSettingRoot.HklmSoftware => $@"HKLM\SOFTWARE\{setting.KeyPath}",
        _ => $@"HKLM\{DisplayClassKey}\<adapter>{(setting.KeyPath.Length == 0 ? string.Empty : $@"\{setting.KeyPath}")}",
    };

    private static string Expand(string path) => Environment.ExpandEnvironmentVariables(path);

    private static void RemoveIfThere(string path)
    {
        if (Directory.Exists(path))
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
    }

    private static async Task StopAsync(string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Already gone.
                }
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    /// <summary>The Windows Installer product codes whose name contains <paramref name="name"/>, in both registry views.</summary>
    private static IEnumerable<string> ProductCodes(string name)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            foreach (var code in uninstall?.GetSubKeyNames() ?? [])
            {
                using var product = uninstall!.OpenSubKey(code);
                if (code.StartsWith('{')
                    && (product?.GetValue("DisplayName") as string)?.Contains(name, StringComparison.OrdinalIgnoreCase) == true)
                {
                    codes.Add(code);
                }
            }
        }

        return codes;
    }

    private void RemoveRunOnce()
    {
        try
        {
            using var runOnce = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(RunOnceKey, writable: true);
            runOnce?.DeleteValue(DriverClean.RunOnceValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not remove the RunOnce value {Name}", DriverClean.RunOnceValueName);
        }
    }

    /// <summary>A path inside <paramref name="root"/>, or an exception for one that would leave it.</summary>
    private static string Inside(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"\"{relative}\" would leave the folder it belongs in, so nothing was done with it.");
        }

        return full;
    }

    private static string FreshFolder(GpuVendor? vendor)
    {
        var folder = Path.Combine(WorkFolder(vendor), DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private void TryDelete(string path)
    {
        try
        {
            RemoveIfThere(path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not tidy away {Path}", path);
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.Referrer = new Uri("https://www.amd.com/");
        return client;
    }
}
