using System.Diagnostics;
using System.Runtime.InteropServices;
using AkariDash.Core.Gpus;
using Microsoft.Win32;

namespace AkariDash.App.Services;

/// <summary>
/// The graphics cards in this PC, read the way Vain Toolbox's GPU page reads them: every present
/// display adapter from SetupAPI, joined to its key under the display class by its driver key, with
/// the PCIe link from the PCI bus's device properties. Restart GPU is pnputil's /restart-device.
/// </summary>
internal static class WindowsGpuCards
{
    private const string ClassRoot = @"SYSTEM\CurrentControlSet\Control\Class\";
    private static Guid displayClass = new("4d36e968-e325-11ce-bfc1-08002be10318");
    private static readonly Guid PciDevice = new("3ab22e31-8264-4b4e-9af5-a8d2d8e33e62");

    /// <summary>Every NVIDIA, AMD and Intel card present, in Windows' order. Empty when none is, or on any failure.</summary>
    public static IReadOnlyList<GpuCard> Read()
    {
        try
        {
            return Adapters().Select(GpuCard.From).OfType<GpuCard>().ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// Restarts the card's driver: the screen goes black for a moment. Returns true when Windows needs
    /// a reboot to finish (pnputil's 3010); throws when it could not restart the card at all.
    /// </summary>
    public static async Task<bool> RestartAsync(GpuCard card)
    {
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "pnputil.exe"))
        {
            ArgumentList = { "/restart-device", card.InstanceId },
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        }) ?? throw new InvalidOperationException("pnputil did not start.");

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return process.ExitCode switch
        {
            0 => false,
            3010 => true,
            _ => throw new InvalidOperationException($"The GPU could not be restarted (pnputil exit code {process.ExitCode}). {output.Trim()}"),
        };
    }

    private static IEnumerable<GpuAdapterRaw> Adapters()
    {
        var set = Native.SetupDiGetClassDevsW(ref displayClass, null, IntPtr.Zero, Native.DigcfPresent);
        if (set == new IntPtr(-1))
        {
            yield break;
        }

        try
        {
            var info = new Native.SpDevinfoData { cbSize = (uint)Marshal.SizeOf<Native.SpDevinfoData>() };
            for (uint index = 0; Native.SetupDiEnumDeviceInfo(set, index, ref info); index++)
            {
                var instanceId = InstanceId(set, ref info);
                var driverKey = DriverKey(set, ref info);
                if (instanceId is null || driverKey is null)
                {
                    continue;
                }

                using var key = Registry.LocalMachine.OpenSubKey(ClassRoot + driverKey, writable: false);
                yield return new GpuAdapterRaw(
                    instanceId,
                    key?.GetValue("MatchingDeviceId") as string,
                    key?.GetValue("DriverDesc") as string,
                    key?.GetValue("DriverVersion") as string,
                    key?.GetValue("DriverDate") as string,
                    key?.GetValue("HardwareInformation.qwMemorySize"),
                    key?.GetValue("HardwareInformation.MemorySize"),
                    PciValue(set, ref info, 9),
                    PciValue(set, ref info, 10),
                    PciValue(set, ref info, 11),
                    PciValue(set, ref info, 12))
                {
                    DriverKey = driverKey,
                    ProviderName = key?.GetValue("ProviderName") as string,
                    BusNumber = BusNumber(set, ref info),
                };
            }
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(set);
        }
    }

    private static string? InstanceId(IntPtr set, ref Native.SpDevinfoData info)
    {
        var buffer = new char[512];
        return Native.SetupDiGetDeviceInstanceIdW(set, ref info, buffer, (uint)buffer.Length, out var length)
            ? new string(buffer, 0, (int)Math.Max(0, length - 1))
            : null;
    }

    // SPDRP_DRIVER: "{4d36e968-...}\0000", the adapter's key under the display class.
    private static string? DriverKey(IntPtr set, ref Native.SpDevinfoData info)
    {
        var buffer = new byte[1024];
        if (!Native.SetupDiGetDeviceRegistryPropertyW(set, ref info, 9, out _, buffer, (uint)buffer.Length, out var size))
        {
            return null;
        }

        return System.Text.Encoding.Unicode.GetString(buffer, 0, (int)size).TrimEnd('\0');
    }

    // SPDRP_BUSNUMBER: the PCI bus, a DWORD.
    private static uint? BusNumber(IntPtr set, ref Native.SpDevinfoData info)
    {
        var buffer = new byte[4];
        return Native.SetupDiGetDeviceRegistryPropertyW(set, ref info, 0x15, out _, buffer, (uint)buffer.Length, out var size) && size >= 4
            ? BitConverter.ToUInt32(buffer)
            : null;
    }

    private static uint? PciValue(IntPtr set, ref Native.SpDevinfoData info, uint pid)
    {
        var key = new Native.DevPropKey { fmtid = PciDevice, pid = pid };
        var buffer = new byte[4];
        return Native.SetupDiGetDevicePropertyW(set, ref info, ref key, out _, buffer, (uint)buffer.Length, out var size, 0) && size >= 4
            ? BitConverter.ToUInt32(buffer)
            : null;
    }

    private static class Native
    {
        public const uint DigcfPresent = 0x2;

        [StructLayout(LayoutKind.Sequential)]
        public struct SpDevinfoData
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DevPropKey
        {
            public Guid fmtid;
            public uint pid;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, string? enumerator, IntPtr parent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref SpDevinfoData info);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref SpDevinfoData info, char[] id, uint size, out uint required);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiGetDeviceRegistryPropertyW(
            IntPtr set, ref SpDevinfoData info, uint property, out uint type, byte[] buffer, uint size, out uint required);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiGetDevicePropertyW(
            IntPtr set, ref SpDevinfoData info, ref DevPropKey key, out uint type, byte[] buffer, uint size, out uint required, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    }
}
