using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
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

    public MachineValue? Read(MachineLocation location) => location switch
    {
        RegistryLocation registry => ReadRegistryValue(registry),
        ServiceLocation service => ReadServiceStartType(service),
        ScheduledTaskLocation task => ReadTaskEnabled(task),
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
            var deviceId = (adapter?.GetValue("MatchingDeviceId") as string)?.ToUpperInvariant() ?? string.Empty;
            GpuVendor? vendor = deviceId switch
            {
                _ when deviceId.Contains("VEN_10DE") => GpuVendor.Nvidia,
                _ when deviceId.Contains("VEN_1002") => GpuVendor.Amd,
                _ when deviceId.Contains("VEN_8086") => GpuVendor.Intel,
                _ => null,
            };

            if (vendor is { } found)
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
        SignedIn: Try(SignedInUser));

    // WMI lists only adapters that are present, unlike the Display class key, which keeps a subkey
    // for every card ever installed. Adapters that are not PCI devices (the Remote Display
    // Adapter, virtual displays for streaming) are not graphics cards and are left out.
    private static List<string> GpuNames()
    {
        var names = new List<string>();

        dynamic locator = Activator.CreateInstance(Type.GetTypeFromProgID("WbemScripting.SWbemLocator", throwOnError: true)!)!;
        dynamic? services = null;
        try
        {
            services = locator.ConnectServer(".", @"root\cimv2");
            foreach (var adapter in services.ExecQuery("SELECT Name, PNPDeviceID FROM Win32_VideoController"))
            {
                if (adapter.PNPDeviceID is string deviceId &&
                    deviceId.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase) &&
                    adapter.Name is string name)
                {
                    names.Add(name);
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

        return names;
    }

    // The installed memory comes from the firmware's memory tables, which many VMs leave empty;
    // then the memory Windows can use is the next best thing.
    private static ulong? InstalledRamKilobytes()
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
                throw new InvalidOperationException("System Restore is turned off on this PC (System Protection is off for its drives).");
            }

            if (result != 0)
            {
                throw new Win32Exception(result);
            }

            if (NewestRestorePoint(services) <= newestBefore)
            {
                throw new InvalidOperationException(
                    "Windows created no new restore point; by default it allows only one a day, and one was made in the last 24 hours.");
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

    private static uint NewestRestorePoint(dynamic services)
    {
        uint newest = 0;
        foreach (var point in services.ExecQuery("SELECT SequenceNumber FROM SystemRestore"))
        {
            newest = Math.Max(newest, (uint)point.SequenceNumber);
        }

        return newest;
    }

    private static RegistryValue? ReadRegistryValue(RegistryLocation location)
    {
        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(location.Key, writable: false);

        var data = key?.GetValue(location.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return data is null ? null : new RegistryValue(key!.GetValueKind(location.Name), data);
    }

    private static void WriteRegistryValue(RegistryLocation location, RegistryValue value)
    {
        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey(location.Key, writable: true);
        key.SetValue(location.Name, value.Data, value.Kind);
    }

    private static void DeleteRegistryValue(RegistryLocation location)
    {
        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(location.Key, writable: true);
        key?.DeleteValue(location.Name, throwOnMissingValue: false);
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
        public const int WtsUserName = 5;
        public const int WtsDomainName = 7;

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
