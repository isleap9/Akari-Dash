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

    // AkariOS installs Nvidia Profile Inspector here; the profile is imported only when it is present.
    private static readonly string ProfileInspector = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        "Nvidia Profile Inspector", "Nvidia Profile Inspector.exe");

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

        if (AppliesSettings(row))
        {
            foreach (var setting in DriverSettings.For(row.Vendor))
            {
                text.AppendLine($"Set {Where(setting)}\\{setting.Name} = {setting.Value} ({setting.Kind})");
            }

            if (row.Vendor == GpuVendor.Nvidia)
            {
                text.AppendLine("Import the NVIDIA profile with Nvidia Profile Inspector, if it is installed");
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
        var latest = await LatestAsync(vendor, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Could not find the latest {vendor.DisplayName()} driver: {vendor.DisplayName()}'s driver source did not answer with one. Nothing was changed.");

        var folder = FreshFolder(vendor);
        try
        {
            var installer = await DownloadAsync(latest.DownloadUrl, folder, progress, cancellationToken);
            var unpacked = Path.Combine(folder, "unpacked");
            progress?.Report($"Unpacking {vendor.DisplayName()} {latest.Version}…");
            await Task.Run(() => Unpack(installer, unpacked), cancellationToken);

            progress?.Report($"Installing {vendor.DisplayName()} {latest.Version}…");
            var outcome = await RunSetupAsync(
                row, Path.Combine(unpacked, "setup.exe"), DriverInstallCommand.Install(row, "setup.exe"), cancellationToken);

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
            return await ConfigureAsync(row, outcome, progress, cancellationToken);
        }
        finally
        {
            TryDelete(folder);
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
        var latest = vendor == GpuVendor.Amd ? await LatestAsync(vendor, cancellationToken) : null;
        if (latest is null)
        {
            logger.LogInformation("Opening {Vendor}'s driver page", vendor);
            Process.Start(new ProcessStartInfo(DriverLookup.Endpoint(vendor).AbsoluteUri) { UseShellExecute = true });
            return DriverStepOutcome.HandedOffToInstaller;
        }

        var folder = FreshFolder(vendor);
        try
        {
            var installer = await DownloadAsync(latest.DownloadUrl, folder, progress, cancellationToken);

            progress?.Report($"Waiting for {vendor.DisplayName()}'s installer to finish…");
            logger.LogInformation("Opening {Installer} and waiting for it", installer);
            using (var process = Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true }))
            {
                if (process is not null)
                {
                    await process.WaitForExitAsync(cancellationToken);
                }
            }

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
    /// Applies the vendor's settings bundle after an install. A setting that will not write does not
    /// undo the install that just happened; the row says the settings were not applied instead.
    /// </summary>
    private async Task<DriverStepOutcome> ConfigureAsync(
        DriverRow row,
        DriverStepOutcome installed,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report($"Applying the {row.Vendor.DisplayName()} settings…");
        var allApplied = true;

        foreach (var setting in DriverSettings.For(row.Vendor))
        {
            try
            {
                Write(row.Vendor, setting);
                logger.LogInformation("Set {Key}\\{Name} = {Value}", Where(setting), setting.Name, setting.Value);
            }
            catch (Exception ex)
            {
                allApplied = false;
                logger.LogError(ex, "Could not set {Key}\\{Name}", Where(setting), setting.Name);
            }
        }

        if (row.Vendor == GpuVendor.Nvidia && !await ImportNvidiaProfileAsync(cancellationToken))
        {
            allApplied = false;
        }

        return allApplied ? installed : DriverStepOutcome.InstalledWithoutSettings;
    }

    private async Task<bool> ImportNvidiaProfileAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(ProfileInspector))
        {
            logger.LogWarning("Nvidia Profile Inspector is not installed at {Path}, so the NVIDIA profile was not imported", ProfileInspector);
            return false;
        }

        // The profile declares UTF-16, so it is written as UTF-16.
        var profile = Path.Combine(WorkFolder(GpuVendor.Nvidia), "akari-dash.nip");
        Directory.CreateDirectory(Path.GetDirectoryName(profile)!);
        await File.WriteAllTextAsync(profile, DriverSettings.NvidiaProfileXml, Encoding.Unicode, cancellationToken);

        try
        {
            var run = await ProcessRunner.RunAsync(ProfileInspector, ["-silentImport", "-silent", profile], cancellationToken);
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
