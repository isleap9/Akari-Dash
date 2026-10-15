using System.Diagnostics;
using System.Text;
using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SharpCompress.Archives.SevenZip;

namespace AkariDash.App.Services;

/// <summary>
/// Finds, downloads, unpacks, installs and configures a graphics driver. An Action backend
/// (ADR-0004): an install runs once and cannot be undone. Everything that decides anything - the
/// sources, the command lines, the strip lists, the settings - is data in Core; this class only does
/// the network, process, archive and registry work. In the Phase 1 build every write refuses.
/// </summary>
public sealed class WindowsDriverService(ILogger<WindowsDriverService> logger) : IDriverService
{
    private const string DisplayClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
    private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";

    // AMD answers the default .NET client with a refusal, so this looks like a browser (Graphics.ps1:228-232).
    private static readonly HttpClient Http = CreateClient();

    public bool IsDryRun => BuildInfo.IsDryRunOnly;

    private static string WorkFolder(GpuVendor vendor) =>
        Path.Combine(Path.GetTempPath(), "Akari-Dash", "drivers", vendor.ToString().ToLowerInvariant());

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

    public string Describe(DriverRow row, string? installerPath = null)
    {
        var text = new StringBuilder();
        if (row.Kind is DriverStepKind.Clean or DriverStepKind.CleanManual)
        {
            text.AppendLine(row.Vendor == GpuVendor.Intel
                ? $"Check that an Intel installer is waiting in {DriverInbox.Folder}"
                : $"Download the latest {row.Vendor.DisplayName()} driver to {StagedFolder(row.Vendor)}, so it is there after the clean");
            text.AppendLine($"Download DDU from {DriverClean.DduDownload} and unpack it to {DduFolder}");
            text.AppendLine($"Write DDU's settings to {DriverClean.DduSettingsFile}, read-only");
            text.AppendLine($"Write {DriverResetState.DefaultPath}");
            text.AppendLine($@"Set HKLM\{RunOnceKey}\{DriverClean.RunOnceValueName} = {DriverClean.RunOnceCommand(Path.Combine(AppContext.BaseDirectory, DriverClean.CompanionExecutable), DriverResetState.DefaultPath)}");
            text.AppendLine("Run bcdedit /set {current} safeboot minimal, then offer to restart");
            text.AppendLine(row.Kind == DriverStepKind.Clean
                ? "In safe mode: bcdedit /deletevalue {current} safeboot, then " + DriverInstallCommand.Describe(DriverClean.DduExecutable, DriverClean.Arguments(row.Vendor)) + ", then restart"
                : "In safe mode: bcdedit /deletevalue {current} safeboot, then open DDU for you, then restart once it closes");
            return text.ToString().TrimEnd();
        }

        if (row.Kind == DriverStepKind.Debloat)
        {
            var plan = installerPath is null ? null : DriverDebloat.Plan(row.Vendor, Path.GetFileName(installerPath));
            if (plan is null)
            {
                return $"Refuse: {Path.GetFileName(installerPath ?? "(no file)")} is not a {row.Vendor.DisplayName()} driver installer.";
            }

            text.AppendLine($"Unpack {installerPath}");
            if (plan.RemoveAfterExtracting.Count > 0)
            {
                text.AppendLine($"Remove {plan.RemoveAfterExtracting.Count} extras: {string.Join(", ", plan.RemoveAfterExtracting)}");
            }

            if (plan.TurnOffInConfig.Count > 0)
            {
                text.AppendLine($"Switch off the components in: {string.Join(", ", plan.TurnOffInConfig)}");
            }

            text.AppendLine("Run " + DriverInstallCommand.Describe(plan.Setup, plan.SetupArguments));
        }
        else if (DriverInstallCommand.IsUnattended(row.Vendor))
        {
            text.AppendLine($"Download the latest {row.Vendor.DisplayName()} driver and unpack it");
            text.AppendLine("Run " + DriverInstallCommand.Describe("setup.exe", DriverInstallCommand.Install(row, "setup.exe")));
        }
        else if (row.Vendor == GpuVendor.Amd)
        {
            text.AppendLine("Download AMD's installer and open it, then wait for it to finish");
        }
        else
        {
            text.AppendLine($"Open {DriverLookup.Endpoint(row.Vendor)}");
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

    public async Task<DriverStepOutcome> InstallAsync(
        DriverRow row,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        RefuseDryRun(row);
        var vendor = row.Vendor;

        if (!DriverInstallCommand.IsUnattended(vendor))
        {
            return await HandOffAsync(row, progress, cancellationToken);
        }

        progress?.Report($"Finding the latest {vendor.DisplayName()} driver…");
        var latest = await LatestAsync(vendor, cancellationToken);
        var staged = Staged(vendor, latest);
        if (latest is null && staged is null)
        {
            throw new InvalidOperationException(
                $"Could not find the latest {vendor.DisplayName()} driver: {vendor.DisplayName()}'s driver source did not answer with one. Nothing was changed.");
        }

        var folder = FreshFolder(vendor);
        try
        {
            // A clean staged the driver before it removed the old one; that copy is used first.
            var installer = staged ?? await DownloadAsync(latest!.DownloadUrl, folder, progress, cancellationToken);
            var name = Path.GetFileName(installer);
            var unpacked = Path.Combine(folder, "unpacked");
            progress?.Report($"Unpacking {name}…");
            await Task.Run(() => Unpack(installer, unpacked), cancellationToken);

            progress?.Report($"Installing {name}…");
            var outcome = await RunSetupAsync(
                row, Path.Combine(unpacked, "setup.exe"), DriverInstallCommand.Install(row, "setup.exe"), cancellationToken);
            TryDelete(StagedFolder(vendor));

            return AppliesSettings(row) ? await ConfigureAsync(row, outcome, progress, cancellationToken) : outcome;
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

        RefuseDryRun(row);

        var folder = FreshFolder(row.Vendor);
        try
        {
            var unpacked = Path.Combine(folder, "unpacked");
            progress?.Report($"Unpacking {Path.GetFileName(installerPath)}…");
            await Task.Run(() => Unpack(installerPath, unpacked), cancellationToken);

            progress?.Report("Removing the extras…");
            foreach (var entry in plan.RemoveAfterExtracting)
            {
                var path = Inside(unpacked, entry);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }

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
    /// Sets up a DDU clean and leaves the PC to restart into it. Everything a way back needs is put
    /// on disk first - DDU, and the replacement driver - so if any of it cannot be fetched the run
    /// stops here, with the old driver still in place. Only then are RunOnce and safe mode set.
    /// </summary>
    public async Task<DriverStepOutcome> CleanAsync(
        DriverRow row,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        RefuseDryRun(row);
        var vendor = row.Vendor;

        var companion = Path.Combine(AppContext.BaseDirectory, DriverClean.CompanionExecutable);
        if (!File.Exists(companion))
        {
            throw new InvalidOperationException($"{DriverClean.CompanionExecutable} is missing from the app's folder, so nothing was changed. Reinstall Akari Dash.");
        }

        if (ReadReset() is { Stage: DriverResetStage.Scheduled })
        {
            throw new InvalidOperationException("A DDU clean is already waiting for the next restart. Restart, or cancel it first.");
        }

        // The way back, first.
        await StageReplacementAsync(vendor, progress, cancellationToken);

        progress?.Report("Downloading DDU…");
        TryDelete(DduFolder);
        Directory.CreateDirectory(ResetFolder);
        var download = await DownloadAsync(DriverClean.DduDownload, ResetFolder, progress, cancellationToken);
        var ddu = await UnpackDduAsync(download, cancellationToken);
        TryDelete(download);

        var settings = Path.Combine(ddu, DriverClean.DduSettingsFile);
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        if (File.Exists(settings))
        {
            File.SetAttributes(settings, FileAttributes.Normal);
        }

        // Read-only, as AkariOS leaves it, so DDU cannot write its own choices back over ours.
        await File.WriteAllTextAsync(settings, DriverClean.DduSettingsXml, cancellationToken);
        File.SetAttributes(settings, FileAttributes.ReadOnly);

        var state = new DriverResetState(vendor, row.Kind == DriverStepKind.CleanManual, ddu, DateTimeOffset.Now, DriverResetStage.Scheduled);
        await File.WriteAllTextAsync(DriverResetState.DefaultPath, state.ToJson(), cancellationToken);

        progress?.Report("Setting the next restart to safe mode…");
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
                $"Could not set the next restart to safe mode (bcdedit {(bcd is null ? "did not start" : $"exit code {bcd.ExitCode}")}), so nothing was changed.");
        }

        logger.LogInformation("DDU clean of {Vendor} scheduled ({Mode}); DDU in {Folder}", vendor, state.Manual ? "manual" : "auto", ddu);
        return DriverStepOutcome.CleanScheduled;
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

    public async Task CancelResetAsync(CancellationToken cancellationToken = default)
    {
        if (IsDryRun)
        {
            return;
        }

        RemoveRunOnce();
        var bcd = await ProcessRunner.RunAsync("bcdedit.exe", ["/deletevalue", "{current}", "safeboot"], cancellationToken);
        logger.LogInformation("Cancelled the DDU clean (bcdedit exit code {ExitCode})", bcd?.ExitCode);
        ForgetReset();
    }

    public void ForgetReset()
    {
        if (IsDryRun)
        {
            return;
        }

        TryDelete(DriverResetState.DefaultPath);
        if (Directory.Exists(DduFolder))
        {
            foreach (var file in Directory.EnumerateFiles(DduFolder, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
        }

        TryDelete(DduFolder);
    }

    public void RestartNow()
    {
        if (IsDryRun)
        {
            return;
        }

        logger.LogInformation("Restarting for the DDU clean");
        Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 5") { UseShellExecute = false, CreateNoWindow = true });
    }

    private const string RunOnceKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";

    private static string ResetFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Akari-Dash", "driver-reset");

    private static string DduFolder => Path.Combine(ResetFolder, "ddu");

    private static string StagedFolder(GpuVendor vendor) =>
        Path.Combine(ResetFolder, "staged", vendor.ToString().ToLowerInvariant());

    /// <summary>The driver a clean staged for this vendor: the one matching the latest version, or any when the source cannot be asked.</summary>
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

    /// <summary>
    /// Puts the replacement driver on disk before anything is removed. NVIDIA's and AMD's come from
    /// their sources; Intel publishes none, so its installer must already be in the drop folder.
    /// </summary>
    /// <summary>The newest installer in the drop folder that is this vendor's own, or <see langword="null"/>.</summary>
    private static string? DroppedInstaller(GpuVendor vendor) =>
        Directory.Exists(DriverInbox.Folder)
            ? DriverInbox.Installers(Directory.EnumerateFiles(DriverInbox.Folder))
                .FirstOrDefault(file => DriverDebloat.Plan(vendor, Path.GetFileName(file)) is not null)
            : null;

    private async Task StageReplacementAsync(GpuVendor vendor, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (vendor == GpuVendor.Intel)
        {
            var dropped = DroppedInstaller(vendor);
            if (dropped is null)
            {
                throw new InvalidOperationException(
                    $"Intel publishes no driver we can fetch, so put the Intel installer (gfx_win_....exe) in {DriverInbox.Folder} first. Nothing was changed.");
            }

            logger.LogInformation("The Intel driver waiting after the clean is {Installer}", dropped);
            return;
        }

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

    /// <summary>
    /// DDU ships as a Nullsoft self-extractor, which unpacks itself silently with its own /S and /D
    /// switches (/D must come last and unquoted). Returns the folder DDU's program landed in.
    /// </summary>
    private async Task<string> UnpackDduAsync(string download, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(DduFolder);
        var run = await ProcessRunner.RunAsync(download, ["/S", $"/D={DduFolder}"], cancellationToken)
            ?? throw new InvalidOperationException("DDU's download could not be started to unpack it, so nothing was changed.");
        logger.LogInformation("DDU unpacked with exit code {ExitCode}", run.ExitCode);

        var program = Directory.EnumerateFiles(DduFolder, DriverClean.DduExecutable, SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new InvalidOperationException($"DDU did not unpack to {DduFolder} (no {DriverClean.DduExecutable}), so nothing was changed.");
        return Path.GetDirectoryName(program)!;
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

    private static bool AppliesSettings(DriverRow row) => row.Kind is DriverStepKind.InstallAndConfigure or DriverStepKind.Debloat;

    private void RefuseDryRun(DriverRow row)
    {
        if (!IsDryRun)
        {
            return;
        }

        // Phase 1 is safe to launch on any PC because it installs nothing. Say what a real run
        // would have done, then refuse rather than quietly claim a success that did not happen.
        logger.LogInformation("Dry Run only: {Row} would have done: {Plan}", row.Id, Describe(row));
        throw new InvalidOperationException($"This is the Dry Run build, so \"{row.Title}\" did nothing.");
    }

    /// <summary>
    /// AMD and Intel cannot be installed unattended, so their own installer or page is opened and
    /// the row says so. AMD's installer is waited for, so its settings land after it, not under it.
    /// </summary>
    private async Task<DriverStepOutcome> HandOffAsync(DriverRow row, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var vendor = row.Vendor;

        // AkariOS's Intel "install and settings" runs an installer the person downloaded, then
        // configures (Graphics.ps1:423-447). Here that installer comes from the drop folder.
        if (vendor == GpuVendor.Intel && row.Kind == DriverStepKind.InstallAndConfigure && DroppedInstaller(vendor) is { } dropped)
        {
            progress?.Report($"Waiting for {Path.GetFileName(dropped)} to finish…");
            logger.LogInformation("Opening {Installer} and waiting for it", dropped);
            using (var process = Process.Start(new ProcessStartInfo(dropped) { UseShellExecute = true }))
            {
                if (process is not null)
                {
                    await process.WaitForExitAsync(cancellationToken);
                }
            }

            var configured = await ConfigureAsync(row, DriverStepOutcome.HandedOffToInstaller, progress, cancellationToken, usedInstaller: dropped);
            return configured == DriverStepOutcome.InstalledWithoutSettings ? configured : DriverStepOutcome.HandedOffToInstaller;
        }

        var latest = vendor == GpuVendor.Amd ? await LatestAsync(vendor, cancellationToken) : null;
        var staged = vendor == GpuVendor.Amd ? Staged(vendor, latest) : null;
        if (latest is null && staged is null)
        {
            logger.LogInformation("Opening {Vendor}'s driver page", vendor);
            Process.Start(new ProcessStartInfo(DriverLookup.Endpoint(vendor).AbsoluteUri) { UseShellExecute = true });
            return DriverStepOutcome.HandedOffToInstaller;
        }

        var folder = FreshFolder(vendor);
        try
        {
            var installer = staged ?? await DownloadAsync(latest!.DownloadUrl, folder, progress, cancellationToken);

            progress?.Report($"Waiting for {vendor.DisplayName()}'s installer to finish…");
            logger.LogInformation("Opening {Installer} and waiting for it", installer);
            using (var process = Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true }))
            {
                if (process is not null)
                {
                    await process.WaitForExitAsync(cancellationToken);
                }
            }

            TryDelete(StagedFolder(vendor));

            if (AppliesSettings(row))
            {
                var outcome = await ConfigureAsync(row, DriverStepOutcome.HandedOffToInstaller, progress, cancellationToken);
                return outcome == DriverStepOutcome.InstalledWithoutSettings ? outcome : DriverStepOutcome.HandedOffToInstaller;
            }

            return DriverStepOutcome.HandedOffToInstaller;
        }
        finally
        {
            TryDelete(folder);
        }
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

    /// <summary>
    /// Unpacks a vendor's self-extracting installer with a managed reader: no 7-Zip is shipped or
    /// installed (design Call 4). Every entry must land inside <paramref name="destination"/>.
    /// </summary>
    private void Unpack(string installer, string destination)
    {
        using var file = File.OpenRead(installer);
        var start = SelfExtractingArchive.FindSevenZipStart(file)
            ?? throw new InvalidOperationException($"{Path.GetFileName(installer)} is not an installer we can unpack.");

        using var archiveStream = new OffsetStream(file, start);
        using var archive = SevenZipArchive.Open(archiveStream);
        using var reader = archive.ExtractAllEntries();

        Directory.CreateDirectory(destination);
        var count = 0;
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory || reader.Entry.Key is not { } key)
            {
                continue;
            }

            var path = Inside(destination, key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var output = File.Create(path);
            reader.WriteEntryTo(output);
            count++;
        }

        logger.LogInformation("Unpacked {Count} files from {Installer}", count, installer);
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

    /// <summary>
    /// Does everything AkariOS does after the driver (<see cref="DriverExtras"/>), in its order:
    /// the tools, the shortcuts, the clean-up, the settings, the profile. A step that fails does not
    /// undo the install that just happened, nor stop the steps after it; the row says not every step
    /// applied instead. Clean-up of something that is not there is not a failure.
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

    private async Task<bool> RunExtraAsync(GpuVendor vendor, DriverExtra extra, string? usedInstaller, CancellationToken cancellationToken)
    {
        logger.LogInformation("{Step}", extra.Describe());
        switch (extra)
        {
            case ApplyRegistrySettings:
                var applied = true;
                foreach (var setting in DriverSettings.For(vendor))
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
                    var to = Expand(move.ToFolder);
                    File.Move(from, Path.Combine(to, Path.GetFileName(from)), overwrite: true);
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
                return await ImportNvidiaProfileAsync(Expand(import.Inspector), cancellationToken);

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

    private async Task<bool> ImportNvidiaProfileAsync(string inspector, CancellationToken cancellationToken)
    {
        if (!File.Exists(inspector))
        {
            logger.LogWarning("Nvidia Profile Inspector is not at {Path}, so the NVIDIA profile was not imported", inspector);
            return false;
        }

        // The profile declares UTF-16, so it is written as UTF-16.
        var profile = Path.Combine(WorkFolder(GpuVendor.Nvidia), "akari-dash.nip");
        Directory.CreateDirectory(Path.GetDirectoryName(profile)!);
        await File.WriteAllTextAsync(profile, DriverSettings.NvidiaProfileXml, Encoding.Unicode, cancellationToken);

        try
        {
            var run = await ProcessRunner.RunAsync(inspector, ["-silentImport", "-silent", profile], cancellationToken);
            if (run?.ExitCode != 0)
            {
                logger.LogWarning("Nvidia Profile Inspector did not import the profile (exit code {ExitCode})", run?.ExitCode);
                return false;
            }

            logger.LogInformation("Imported the NVIDIA profile");
            return true;
        }
        finally
        {
            TryDelete(profile);
        }
    }

    private static void Write(GpuVendor vendor, DriverSetting setting)
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
                throw new InvalidOperationException($"There is no {vendor.DisplayName()} adapter key to write {setting.Name} under.");
            }

            return;
        }

        using var root = setting.Root == DriverSettingRoot.Hkcu
            ? RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64)
            : RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        var path = setting.Root == DriverSettingRoot.HklmServices ? $@"{ServicesKey}\{setting.KeyPath}" : setting.KeyPath;
        using var target = root.CreateSubKey(path, writable: true);
        SetValue(target, setting);
    }

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

    /// <summary>The vendor's numbered adapter keys under the Display class. The caller disposes each.</summary>
    private static IEnumerable<RegistryKey> Adapters(RegistryKey machine, GpuVendor vendor, bool writable)
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

            if (GpuVendorExtensions.FromMatchingDeviceId(adapter.GetValue("MatchingDeviceId") as string) == vendor)
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
        _ => $@"HKLM\{DisplayClassKey}\<adapter>{(setting.KeyPath.Length == 0 ? string.Empty : $@"\{setting.KeyPath}")}",
    };

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

    private static string FreshFolder(GpuVendor vendor)
    {
        var folder = Path.Combine(WorkFolder(vendor), DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
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

    /// <summary>A read-only view of a stream from <paramref name="start"/> on, so the archive reader sees only the archive.</summary>
    private sealed class OffsetStream(Stream inner, long start) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => inner.Length - start;

        public override long Position
        {
            get => inner.Position - start;
            set => inner.Position = value + start;
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => origin switch
        {
            SeekOrigin.Begin => inner.Seek(offset + start, SeekOrigin.Begin) - start,
            SeekOrigin.Current => inner.Seek(offset, SeekOrigin.Current) - start,
            _ => inner.Seek(offset, SeekOrigin.End) - start,
        };

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
