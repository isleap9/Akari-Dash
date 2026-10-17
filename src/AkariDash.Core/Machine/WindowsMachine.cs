using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;

namespace AkariDash.Core.Machine;

/// <summary>
/// The real machine: the Windows registry, the Service Control Manager, the Task Scheduler,
/// System Restore, WMI and Remote Desktop Services (for who is signed in).
/// Verified manually in a VM only.
/// </summary>
public sealed class WindowsMachine : IMachine
{
    private const string DisplayClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string ProcessorKey = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";
    private const string BiosKey = @"HARDWARE\DESCRIPTION\System\BIOS";
    private const string RunOnceKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";

    public MachineValue? Read(MachineLocation location) => location switch
    {
        RegistryLocation registry => ReadRegistryValue(registry),
        ServiceLocation service => ReadServiceStartType(service),
        ScheduledTaskLocation task => ReadTaskEnabled(task),
        RegistryBitLocation bit => bit.ReadFrom(ReadRegistryValue(bit.Value)),
        BootSettingLocation boot => ReadBootSetting(boot),
        RegistryEntryLocation entry => entry.ReadFrom(ReadRegistryValue(entry.Value)),
        RegistryKeyLocation key => ReadRegistryKey(key),
        BlackImageLocation image => BlackImages.ReadFile(image),
        AccountPicturesLocation => BlackImages.ReadAccountPictures(),
        _ => throw Unsupported(location),
    };

    public void Write(MachineLocation location, MachineValue value)
    {
        switch (location, value)
        {
            case (RegistryLocation registry, RegistryValue data):
                WriteRegistryValue(registry, data);
                break;
            case (ServiceLocation service, ServiceStartValue start):
                WriteServiceStartType(service, start.StartType);
                break;
            case (ScheduledTaskLocation task, TaskEnabledValue enabled):
                WriteTaskEnabled(task, enabled.Enabled);
                break;
            case (RegistryBitLocation bit, BitValue set):
                WriteRegistryValue(bit.Value, bit.WriteInto(ReadRegistryValue(bit.Value), set.IsSet));
                break;
            case (RegistryEntryLocation entry, EntryValue text):
                WriteRegistryEntry(entry, text.Text);
                break;
            case (BootSettingLocation boot, BootSettingValue setting):
                RunBcdedit("/set", "{current}", boot.Name, setting.Text);
                break;
            case (RegistryKeyLocation key, KeyPresence):
                CreateRegistryKey(key);
                break;
            case (BlackImageLocation image, BlackImage):
                BlackImages.WriteFile(image);
                break;
            case (AccountPicturesLocation, BlackImage):
                BlackImages.PaintAccountPictures();
                break;
            default:
                throw new ArgumentException($"{location} cannot hold {value.GetType().Name}.", nameof(value));
        }
    }

    public void Delete(MachineLocation location)
    {
        if (location is RegistryLocation registry)
        {
            DeleteRegistryValue(registry);
        }
        else if (location is RegistryEntryLocation entry)
        {
            WriteRegistryEntry(entry, null);
        }
        else if (location is RegistryKeyLocation key)
        {
            DeleteRegistryKey(key);
        }
        else if (location is BlackImageLocation image)
        {
            BlackImages.DeleteFile(image);
        }
        else if (location is AccountPicturesLocation)
        {
            BlackImages.RestoreAccountPictures();
        }
        else if (location is BootSettingLocation boot)
        {
            // bcdedit fails on a setting that is not there, so a missing one is left alone.
            if (ReadBootSetting(boot) is not null)
            {
                RunBcdedit("/deletevalue", "{current}", boot.Name);
            }
        }
        else if (Read(location) is not null)
        {
            throw new NotSupportedException($"{location} cannot be deleted.");
        }
    }

    // Every display adapter Windows has a driver for gets a numbered subkey of the Display class
    // key, whose MatchingDeviceId names its PCI vendor (e.g. pci\ven_10de&dev_2484). Adapters that
    // are not PCI cards, such as the Basic Display Adapter, are skipped.
    public IReadOnlySet<GpuVendor> GpuVendors()
    {
        var vendors = new HashSet<GpuVendor>();

        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var displayClass = baseKey.OpenSubKey(DisplayClassKey, writable: false);
        foreach (var name in displayClass?.GetSubKeyNames() ?? [])
        {
            if (!int.TryParse(name, out _))
            {
                continue;
            }

            using var adapter = displayClass!.OpenSubKey(name, writable: false);
            if (GpuVendorExtensions.FromMatchingDeviceId(adapter?.GetValue("MatchingDeviceId") as string) is { } found)
            {
                vendors.Add(found);
            }
        }

        return vendors;
    }

    // Each fact is read on its own, so one Windows cannot report leaves the others intact.
    public PcFacts DescribePc() => new(
        ProductName: Try(() => ReadLocalMachineValue(CurrentVersionKey, "ProductName") as string),
        // DisplayVersion (e.g. 24H2) replaced ReleaseId (e.g. 2004) in Windows 10 20H2.
        DisplayVersion: Try(() =>
            ReadLocalMachineValue(CurrentVersionKey, "DisplayVersion") as string ??
            ReadLocalMachineValue(CurrentVersionKey, "ReleaseId") as string),
        Build: Try(() => int.TryParse(ReadLocalMachineValue(CurrentVersionKey, "CurrentBuildNumber") as string, out var build) ? build : (int?)null),
        Revision: Try(() => ReadLocalMachineValue(CurrentVersionKey, "UBR") as int?),
        Cpu: Try(() => ReadLocalMachineValue(ProcessorKey, "ProcessorNameString") as string),
        Gpus: Try(GpuNames) ?? [],
        InstalledRamKilobytes: Try(InstalledRamKilobytes),
        RunningAs: Try(() => WindowsIdentity.GetCurrent().Name),
        SignedIn: Try(SignedInUser),
        BoardMaker: Try(() => ReadLocalMachineValue(BiosKey, "BaseBoardManufacturer") as string),
        BoardModel: Try(() => ReadLocalMachineValue(BiosKey, "BaseBoardProduct") as string),
        Disks: Try(Disks),
        Displays: Try(Displays));

    private static List<DiskFact> Disks() =>
        QueryWmi("SELECT Model, Size FROM Win32_DiskDrive", disk =>
            WmiProperty(disk, "Model") is string model
                ? new DiskFact(model, ulong.TryParse(Convert.ToString(WmiProperty(disk, "Size"), CultureInfo.InvariantCulture), out var size) ? size : null)
                : null);

    // The mode each graphics card is drawing now, as Win32_VideoController reports it.
    private static List<DisplayFact> Displays() =>
        QueryWmi("SELECT PNPDeviceID, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController", adapter =>
            IsGraphicsCard(adapter) &&
            WmiProperty(adapter, "CurrentHorizontalResolution") is { } width &&
            WmiProperty(adapter, "CurrentVerticalResolution") is { } height
                ? new DisplayFact(
                    Convert.ToInt32(width, CultureInfo.InvariantCulture),
                    Convert.ToInt32(height, CultureInfo.InvariantCulture),
                    WmiProperty(adapter, "CurrentRefreshRate") is { } hz ? Convert.ToInt32(hz, CultureInfo.InvariantCulture) : null)
                : null);

    // Runs a WMI query in root\cimv2 and keeps what select makes of each object (null skips it).
    private static List<T> QueryWmi<T>(string query, Func<object, T?> select)
        where T : class
    {
        var results = new List<T>();

        dynamic locator = Activator.CreateInstance(Type.GetTypeFromProgID("WbemScripting.SWbemLocator", throwOnError: true)!)!;
        dynamic? services = null;
        try
        {
            services = locator.ConnectServer(".", @"root\cimv2");
            foreach (var item in services.ExecQuery(query))
            {
                if (select((object)item) is T result)
                {
                    results.Add(result);
                }
            }
        }
        finally
        {
            foreach (var comObject in new object?[] { services, locator })
            {
                if (comObject is not null)
                {
                    Marshal.FinalReleaseComObject(comObject);
                }
            }
        }

        return results;
    }

    // WMI lists only adapters that are present, unlike the Display class key, which keeps a subkey
    // for every card ever installed. Adapters that are not PCI devices (the Remote Display
    // Adapter, virtual displays for streaming) are not graphics cards and are left out.
    private static List<string> GpuNames() =>
        QueryWmi("SELECT Name, PNPDeviceID FROM Win32_VideoController", adapter =>
            IsGraphicsCard(adapter) ? WmiProperty(adapter, "Name") as string : null);

    private static bool IsGraphicsCard(object adapter) =>
        WmiProperty(adapter, "PNPDeviceID") is string deviceId &&
        deviceId.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase);

    // The installed memory comes from the firmware's memory tables, which many VMs leave empty;
    // then the memory Windows can use is the next best thing. Also used by the svchost split Tweak.
    internal static ulong? InstalledRamKilobytes()
    {
        if (Native.GetPhysicallyInstalledSystemMemory(out var kilobytes))
        {
            return kilobytes;
        }

        var status = new Native.MemoryStatusEx { Length = (uint)Marshal.SizeOf<Native.MemoryStatusEx>() };
        return Native.GlobalMemoryStatusEx(ref status) ? status.TotalPhys / 1024 : null;
    }

    private static object? ReadLocalMachineValue(string keyPath, string name)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(keyPath, writable: false);
        return key?.GetValue(name);
    }

    // The user signed in to the session this process runs in. Elevating with another account's
    // credentials keeps the process in the signed-in user's session, so this tells the two apart.
    private static string? SignedInUser()
    {
        var session = Process.GetCurrentProcess().SessionId;
        var user = SessionText(session, Native.WtsUserName);
        if (string.IsNullOrEmpty(user))
        {
            return null;
        }

        var domain = SessionText(session, Native.WtsDomainName);
        var account = new NTAccount(string.IsNullOrEmpty(domain) ? user : $@"{domain}\{user}");

        // Spelled the way the account's SID resolves, as WindowsIdentity names the running account,
        // since the session's domain part can differ (e.g. for Microsoft Entra ID accounts).
        try
        {
            return account.Translate(typeof(SecurityIdentifier)).Translate(typeof(NTAccount)).Value;
        }
        catch (IdentityNotMappedException)
        {
            return account.Value;
        }
    }

    private static string? SessionText(int session, int infoClass)
    {
        if (!Native.WTSQuerySessionInformationW(IntPtr.Zero, session, infoClass, out var buffer, out _))
        {
            throw new Win32Exception();
        }

        try
        {
            return Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            Native.WTSFreeMemory(buffer);
        }
    }

    private static T? Try<T>(Func<T?> read)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return default;
        }
    }

    // System Restore's WMI provider (late bound, like the Task Scheduler). Windows creates at most
    // one restore point a day by default and then skips the request while still reporting success,
    // so success counts only when a new restore point actually appears.
    public void CreateRestorePoint(string description)
    {
        // Windows answers a restore point request on an unprotected drive inconsistently (1058, or a
        // bare E_FAIL), so check System Protection first.
        if (!IsSystemProtectionOn(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\"))
        {
            throw new SystemProtectionOffException();
        }

        dynamic locator = Activator.CreateInstance(Type.GetTypeFromProgID("WbemScripting.SWbemLocator", throwOnError: true)!)!;
        dynamic? services = null;
        dynamic? restore = null;
        try
        {
            services = locator.ConnectServer(".", @"root\default");
            var newestBefore = NewestRestorePoint(services);

            restore = services.Get("SystemRestore");
            int result;
            try
            {
                result = (int)restore.CreateRestorePoint(description, Native.ModifySettings, Native.BeginSystemChange);
            }
            catch (COMException ex) when (ex.HResult == Native.ErrorServiceDisabledHResult)
            {
                result = Native.ErrorServiceDisabled;
            }

            if (result == Native.ErrorServiceDisabled)
            {
                throw new SystemProtectionOffException();
            }

            if (result != 0)
            {
                throw new Win32Exception(result);
            }

            if (WaitForNewRestorePoint(services, newestBefore) is { } newestAfter)
            {
                throw new InvalidOperationException(
                    $"Windows reported success but no new restore point appeared within {Native.RestorePointAppearsSeconds} "
                        + $"seconds (the newest was {newestBefore} before the request and {newestAfter} after); by default "
                        + "Windows allows only one a day, and one was made in the last 24 hours.");
            }
        }
        finally
        {
            foreach (var comObject in new object?[] { restore, services, locator })
            {
                if (comObject is not null)
                {
                    Marshal.FinalReleaseComObject(comObject);
                }
            }
        }
    }

    // System Restore lists every protected drive in SPP\Clients, as entries such as
    // "\\?\Volume{...}\:(C%3A)"; a drive missing from it has System Protection off.
    private static bool IsSystemProtectionOn(string drive)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var clients = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SPP\Clients");
        var marker = $"({drive.TrimEnd('\\', ':')}%3A)";
        return clients?.GetValueNames()
            .Select(name => clients.GetValue(name))
            .OfType<string[]>()
            .SelectMany(entries => entries)
            .Any(entry => entry.Contains(marker, StringComparison.OrdinalIgnoreCase)) ?? false;
    }

    public void TurnOnSystemProtection(string drive)
    {
        dynamic locator = Activator.CreateInstance(Type.GetTypeFromProgID("WbemScripting.SWbemLocator", throwOnError: true)!)!;
        dynamic? services = null;
        dynamic? restore = null;
        try
        {
            services = locator.ConnectServer(".", @"root\default");
            restore = services.Get("SystemRestore");
            var result = (int)restore.Enable(drive);
            if (result != 0)
            {
                throw new Win32Exception(result);
            }
        }
        finally
        {
            foreach (var comObject in new object?[] { restore, services, locator })
            {
                if (comObject is not null)
                {
                    Marshal.FinalReleaseComObject(comObject);
                }
            }
        }
    }

    // The safe-mode half of a Tweak Windows only lets be written in safe boot. The pass joins the
    // queue waiting for the next restart rather than replacing it, so two safe-mode Tweaks staged
    // before one restart both run. Every piece the companion needs is written to disk and registered
    // before the PC is told to restart, and each step that can fail takes the earlier ones back, so a
    // failure leaves the PC as it was — never restarting into safe mode with nothing there to do.
    public void StageSafeModePass(SafeModePass pass)
    {
        var companion = Path.Combine(AppContext.BaseDirectory, DefenderOptimize.CompanionExecutable);
        if (!File.Exists(companion))
        {
            throw new FileNotFoundException(
                $"{DefenderOptimize.CompanionExecutable} is not next to Akari-Dash, so nothing was staged.", companion);
        }

        SafeModePass.Append(SafeModePass.DefaultPath, pass);

        try
        {
            using var runOnce = RegistryKey
                .OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .CreateSubKey(RunOnceKey, writable: true)
                ?? throw new InvalidOperationException("Windows' RunOnce key could not be opened.");
            runOnce.SetValue(
                DefenderOptimize.RunOnceValueName,
                DefenderOptimize.RunOnceCommand(companion, SafeModePass.DefaultPath),
                RegistryValueKind.String);
        }
        catch (Exception)
        {
            // A failed staging takes back this Tweak's own pass, never another Tweak's: one may still
            // be waiting, and its RunOnce entry has to stay to launch it. Only with nothing still
            // waiting is there nothing left for RunOnce to start, and the entry goes with it.
            SafeModePass.RemoveTweak(SafeModePass.DefaultPath, pass.TweakId);
            if (!SafeModePass.ReadList(SafeModePass.DefaultPath)
                .Any(pending => pending.Stage == SafeModePassStage.Scheduled))
            {
                RemoveRunOnceValue();
            }

            throw;
        }

        try
        {
            // The flag decides only how the NEXT start goes, so this session carries on untouched.
            // Set before the restart is asked for: if this fails, the pass and its RunOnce entry are
            // taken back, so the PC is never left set to boot into safe mode for nothing.
            RunBcdedit("/set", "{current}", "safeboot", "minimal");
        }
        catch (Exception)
        {
            // As above: this Tweak's pass goes, but another Tweak's pending pass keeps the RunOnce
            // entry it would otherwise lose.
            SafeModePass.RemoveTweak(SafeModePass.DefaultPath, pass.TweakId);
            if (!SafeModePass.ReadList(SafeModePass.DefaultPath)
                .Any(pending => pending.Stage == SafeModePassStage.Scheduled))
            {
                RemoveRunOnceValue();
            }

            throw;
        }

        // The restart is left a short delay so the change is on screen before the PC goes down. A
        // restart the user starts themselves does just as well: RunOnce runs at the next sign-in.
        using var restart = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "/r", "/t", "20", "/c", "Akari-Dash is restarting to apply a change in safe mode" },
        });
    }

    private static void RemoveRunOnceValue()
    {
        try
        {
            using var runOnce = RegistryKey
                .OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(RunOnceKey, writable: true);
            runOnce?.DeleteValue(DefenderOptimize.RunOnceValueName, throwOnMissingValue: false);
        }
        catch (Exception)
        {
            // Harmless on its own: with nothing waiting in the pass file, the companion does nothing.
        }
    }

    // A created restore point can take a few seconds to turn up in the store after Windows has
    // already answered the request: on this PC the point was stamped 20:06:26 and the request came
    // back at 20:06:32, and a read right then still did not list it. So the answer is waited for,
    // and only a wait that runs out means Windows really skipped the request. Answers the newest
    // sequence number still seen when it ran out, or null once the new restore point appeared.
    private static uint? WaitForNewRestorePoint(dynamic services, uint newestBefore)
    {
        uint newest;
        for (var attempt = 0; ; attempt++)
        {
            newest = NewestRestorePoint(services);
            if (newest > newestBefore)
            {
                return null;
            }

            if (attempt >= Native.RestorePointAppearsSeconds)
            {
                return newest;
            }

            Thread.Sleep(TimeSpan.FromSeconds(1));
        }
    }

    private static uint NewestRestorePoint(dynamic services)
    {
        uint newest = 0;
        foreach (var point in services.ExecQuery("SELECT SequenceNumber FROM SystemRestore"))
        {
            newest = Math.Max(newest, Convert.ToUInt32(WmiProperty(point, "SequenceNumber"), CultureInfo.InvariantCulture));
        }

        return newest;
    }

    // A WMI object's own properties are reachable only through SWbemObject's Properties_ collection.
    // Asking for one by name directly (point.SequenceNumber) answers E_FAIL, which a late-bound get
    // throws as COMException 0x80004005 and which PowerShell quietly reports as an empty value, so
    // reading the newest restore point killed the restore point itself on real hardware.
    private static object? WmiProperty(dynamic wmiObject, string name) => wmiObject.Properties_.Item(name).Value;

    // Rewrites the whole string with only this entry changed; removing the last entry deletes the value.
    private static void WriteRegistryEntry(RegistryEntryLocation entry, string? text)
    {
        if (entry.WriteInto(ReadRegistryValue(entry.Value), text) is { } whole)
        {
            WriteRegistryValue(entry.Value, whole);
        }
        else
        {
            DeleteRegistryValue(entry.Value);
        }
    }

    // Some keys (such as SmartScreen's WTDS\Components) let only SYSTEM or TrustedInstaller read
    // them; that is reported naming the key, so the Tweak can show it as Unavailable.
    private static RegistryValue? ReadRegistryValue(RegistryLocation location)
    {
        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, RegistryView.Registry64);
        RegistryKey? key;
        try
        {
            key = baseKey.OpenSubKey(location.Key, writable: false);
        }
        catch (System.Security.SecurityException ex)
        {
            throw new UnauthorizedAccessException($"Windows does not let Akari-Dash read {location}.", ex);
        }

        using var _ = key;

        var data = key?.GetValue(location.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return data is null ? null : new RegistryValue(key!.GetValueKind(location.Name), data);
    }

    private static void WriteRegistryValue(RegistryLocation location, RegistryValue value)
    {
        if (IsServiceOwned(location))
        {
            WithRestorePrivilege(location, key => key.SetValue(location.Name, value.Data, value.Kind));
            return;
        }

        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey(location.Key, writable: true);
        key.SetValue(location.Name, value.Data, value.Kind);
    }

    private static KeyPresence? ReadRegistryKey(RegistryKeyLocation location)
    {
        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(location.Key);
        return key is null ? null : KeyPresence.Present;
    }

    private static void CreateRegistryKey(RegistryKeyLocation location)
    {
        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey(location.Key, writable: false);
    }

    // The whole tree: the key's presence is the setting, and a subkey left behind (the Windows 10
    // menu's InprocServer32) would keep it in force. A missing key is already deleted.
    private static void DeleteRegistryKey(RegistryKeyLocation location)
    {
        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, RegistryView.Registry64);
        baseKey.DeleteSubKeyTree(location.Key, throwOnMissingSubKey: false);
    }

    private static void DeleteRegistryValue(RegistryLocation location)
    {
        if (IsServiceOwned(location))
        {
            WithRestorePrivilege(location, key => key.DeleteValue(location.Name, throwOnMissingValue: false));
            return;
        }

        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(location.Key, writable: true);
        key?.DeleteValue(location.Name, throwOnMissingValue: false);
    }

    // Keys that belong to a Windows service: Administrators may only read them, and Settings changes
    // them through the service. Akari-Dash writes these values, and only these, with the restore
    // privilege every administrator has (as backup tools do); the key's permissions stay as they are.
    private static readonly string[] ServiceOwnedKeys =
    [
        // Windows Search: Find my files (Classic or Enhanced).
        @"SOFTWARE\Microsoft\Windows Search\Gather\Windows\SystemIndex",
    ];

    private static bool IsServiceOwned(RegistryLocation location) =>
        location.Hive == RegistryHive.LocalMachine
        && ServiceOwnedKeys.Contains(location.Key, StringComparer.OrdinalIgnoreCase);

    // Turns SeRestorePrivilege on for this process only while the key is open, and opens the key with
    // REG_OPTION_BACKUP_RESTORE, which grants write access through that privilege.
    private static void WithRestorePrivilege(RegistryLocation location, Action<RegistryKey> write)
    {
        SetPrivilege(Native.SeRestorePrivilege, enable: true);
        try
        {
            var result = Native.RegCreateKeyExW(
                Native.HkeyLocalMachine, location.Key, 0, null, Native.RegOptionBackupRestore,
                Native.KeySetValue | Native.KeyQueryValue | Native.KeyWow6464Key, IntPtr.Zero, out var handle, out _);
            if (result != 0)
            {
                throw new UnauthorizedAccessException(
                    $@"Windows did not let Akari-Dash change HKLM\{location.Key}: {new Win32Exception(result).Message}");
            }

            using var key = RegistryKey.FromHandle(handle, RegistryView.Registry64);
            write(key);
        }
        finally
        {
            SetPrivilege(Native.SeRestorePrivilege, enable: false);
        }
    }

    private static void SetPrivilege(string privilege, bool enable)
    {
        if (!Native.OpenProcessToken(Native.GetCurrentProcess(), Native.TokenAdjustPrivileges | Native.TokenQuery, out var token))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            if (!Native.LookupPrivilegeValueW(null, privilege, out var luid))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var state = new Native.TokenPrivileges
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = enable ? Native.SePrivilegeEnabled : 0,
            };
            if (!Native.AdjustTokenPrivileges(token, false, ref state, 0, IntPtr.Zero, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            // AdjustTokenPrivileges succeeds even when the token lacks the privilege (not elevated).
            if (enable && Marshal.GetLastWin32Error() == Native.ErrorNotAllAssigned)
            {
                throw new UnauthorizedAccessException($"Akari-Dash needs to run as administrator to change this setting ({privilege} is missing).");
            }
        }
        finally
        {
            Native.CloseHandle(token);
        }
    }

    // The Service Control Manager keeps each service's configuration under its Services key, so
    // reading it there sees exactly what ChangeServiceConfig wrote.
    private static ServiceStartValue? ReadServiceStartType(ServiceLocation location)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{location.ServiceName}", writable: false);
        if (key?.GetValue("Start") is not int start)
        {
            return null;
        }

        var delayed = key.GetValue("DelayedAutostart") is 1;
        return new ServiceStartValue(start switch
        {
            0 => ServiceStartType.Boot,
            1 => ServiceStartType.System,
            2 when delayed => ServiceStartType.AutomaticDelayed,
            2 => ServiceStartType.Automatic,
            3 => ServiceStartType.Manual,
            4 => ServiceStartType.Disabled,
            _ => throw new InvalidDataException($"{location} has an unknown start type: {start}."),
        });
    }

    // Goes through the Service Control Manager rather than the registry, so the change takes
    // effect without a restart and protected services refuse it instead of being corrupted.
    private static void WriteServiceStartType(ServiceLocation location, ServiceStartType startType)
    {
        var manager = Native.OpenSCManagerW(null, null, Native.ScManagerConnect);
        if (manager == IntPtr.Zero)
        {
            throw new Win32Exception();
        }

        try
        {
            var service = Native.OpenServiceW(manager, location.ServiceName, Native.ServiceChangeConfig);
            if (service == IntPtr.Zero)
            {
                throw new Win32Exception();
            }

            try
            {
                var start = startType switch
                {
                    ServiceStartType.Boot => 0u,
                    ServiceStartType.System => 1u,
                    ServiceStartType.Automatic or ServiceStartType.AutomaticDelayed => 2u,
                    ServiceStartType.Manual => 3u,
                    _ => 4u,
                };

                if (!Native.ChangeServiceConfigW(
                        service, Native.ServiceNoChange, start, Native.ServiceNoChange,
                        null, null, IntPtr.Zero, null, null, null, null))
                {
                    throw new Win32Exception();
                }

                // Delayed start only means anything for automatic services.
                if (start == 2)
                {
                    var info = new Native.ServiceDelayedAutoStartInfo
                    {
                        DelayedAutostart = startType == ServiceStartType.AutomaticDelayed,
                    };
                    if (!Native.ChangeServiceConfig2W(service, Native.ServiceConfigDelayedAutoStartInfo, ref info))
                    {
                        throw new Win32Exception();
                    }
                }
            }
            finally
            {
                Native.CloseServiceHandle(service);
            }
        }
        finally
        {
            Native.CloseServiceHandle(manager);
        }
    }

    private static TaskEnabledValue? ReadTaskEnabled(ScheduledTaskLocation location) =>
        WithTask(location, task => new TaskEnabledValue((bool)task.Enabled), missing: null);

    private static void WriteTaskEnabled(ScheduledTaskLocation location, bool enabled)
    {
        var found = WithTask(location, task =>
        {
            task.Enabled = enabled;
            return true;
        }, missing: false);

        if (!found)
        {
            throw new InvalidOperationException($"{location} does not exist.");
        }
    }

    // The Task Scheduler's COM API (ITaskService), late bound so no interop assembly is needed.
    private static T WithTask<T>(ScheduledTaskLocation location, Func<dynamic, T> use, T missing)
    {
        var separator = location.Path.LastIndexOf('\\');
        var folderPath = separator <= 0 ? @"\" : location.Path[..separator];
        var name = location.Path[(separator + 1)..];

        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", throwOnError: true)!)!;
        dynamic? folder = null;
        dynamic? task = null;
        try
        {
            service.Connect();

            try
            {
                folder = service.GetFolder(folderPath);
                task = folder.GetTask(name);
            }
            // .NET turns these HRESULTs into FileNotFoundException and DirectoryNotFoundException,
            // not COMException, and the message (sometimes a stale one) is not to be trusted.
            catch (Exception ex) when (
                ex is FileNotFoundException or DirectoryNotFoundException ||
                ex.HResult is Native.ErrorFileNotFound or Native.ErrorPathNotFound)
            {
                return missing;
            }

            return use(task);
        }
        finally
        {
            foreach (var comObject in new object?[] { task, folder, service })
            {
                if (comObject is not null)
                {
                    Marshal.FinalReleaseComObject(comObject);
                }
            }
        }
    }

    // bcdedit /enum {current} lists one setting per line: its name, spaces, then its value. Names are
    // never translated; Yes/No may be, but the text read is what is written back on Undo.
    private static BootSettingValue? ReadBootSetting(BootSettingLocation location)
    {
        foreach (var line in RunBcdedit("/enum", "{current}").Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            var nameEnd = trimmed.IndexOf(' ');
            if (nameEnd > 0 && trimmed[..nameEnd].Equals(location.Name, StringComparison.OrdinalIgnoreCase))
            {
                return new BootSettingValue(trimmed[nameEnd..].Trim());
            }
        }

        return null;
    }

    // Runs System32's own bcdedit, never one found on PATH, and fails with its message.
    private static string RunBcdedit(params string[] arguments)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "bcdedit.exe"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("bcdedit could not be started.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0
            ? output
            : throw new InvalidOperationException($"bcdedit {string.Join(' ', arguments)} failed: {(error + output).Trim()}");
    }

    private static NotSupportedException Unsupported(MachineLocation location) =>
        new($"Unknown kind of location: {location}");

    private static class Native
    {
        public const uint ScManagerConnect = 0x0001;
        public const uint ServiceChangeConfig = 0x0002;
        public const uint ServiceNoChange = 0xFFFFFFFF;
        public const uint ServiceConfigDelayedAutoStartInfo = 3;
        public const int ErrorFileNotFound = unchecked((int)0x80070002);
        public const int ErrorPathNotFound = unchecked((int)0x80070003);
        public const int ErrorServiceDisabled = 1058;
        public const int ErrorServiceDisabledHResult = unchecked((int)0x80070422);
        public const int ModifySettings = 12;
        public const int BeginSystemChange = 100;
        public const int RestorePointAppearsSeconds = 20;
        public const int WtsUserName = 5;
        public const int WtsDomainName = 7;
        public const string SeRestorePrivilege = "SeRestorePrivilege";
        public const uint SePrivilegeEnabled = 0x2;
        public const int ErrorNotAllAssigned = 1300;
        public const uint TokenAdjustPrivileges = 0x20;
        public const uint TokenQuery = 0x8;
        public const uint RegOptionBackupRestore = 0x4;
        public const int KeyQueryValue = 0x1;
        public const int KeySetValue = 0x2;
        public const int KeyWow6464Key = 0x100;
        public static readonly IntPtr HkeyLocalMachine = unchecked((IntPtr)(int)0x80000002);

        [StructLayout(LayoutKind.Sequential)]
        public struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TokenPrivileges
        {
            public uint PrivilegeCount;
            public Luid Luid;
            public uint Attributes;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenProcessToken(IntPtr process, uint desiredAccess, out IntPtr token);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LookupPrivilegeValueW(string? systemName, string name, out Luid luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AdjustTokenPrivileges(
            IntPtr token, [MarshalAs(UnmanagedType.Bool)] bool disableAll, ref TokenPrivileges newState,
            uint bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern int RegCreateKeyExW(
            IntPtr key, string subKey, int reserved, string? className, uint options, int desired,
            IntPtr securityAttributes, out Microsoft.Win32.SafeHandles.SafeRegistryHandle result, out int disposition);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceDelayedAutoStartInfo
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool DelayedAutostart;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr OpenSCManagerW(string? machineName, string? databaseName, uint desiredAccess);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr OpenServiceW(IntPtr manager, string serviceName, uint desiredAccess);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ChangeServiceConfigW(
            IntPtr service, uint serviceType, uint startType, uint errorControl,
            string? binaryPathName, string? loadOrderGroup, IntPtr tagId, string? dependencies,
            string? serviceStartName, string? password, string? displayName);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ChangeServiceConfig2W(IntPtr service, uint infoLevel, ref ServiceDelayedAutoStartInfo info);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseServiceHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);

        [StructLayout(LayoutKind.Sequential)]
        public struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

        [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WTSQuerySessionInformationW(IntPtr server, int sessionId, int infoClass, out IntPtr buffer, out int bytesReturned);

        [DllImport("wtsapi32.dll")]
        public static extern void WTSFreeMemory(IntPtr memory);
    }
}
