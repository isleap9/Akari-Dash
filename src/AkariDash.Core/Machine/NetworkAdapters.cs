using System.Net.NetworkInformation;

namespace AkariDash.Core.Machine;

/// <summary>A network adapter by its interface GUID (as in Tcpip\Parameters\Interfaces) and its names.</summary>
public sealed record NetworkAdapter(string Id, string Name, string Description);

/// <summary>The network adapters per-adapter Tweaks (DNS servers, Nagle's algorithm) apply to.</summary>
public static class NetworkAdapters
{
    // Virtual adapters (a VM host's own networks, VPN tunnels) get their settings from the software
    // that made them, so they are left to it. Host-side VM adapters all say "Virtual" (Hyper-V Virtual
    // Ethernet, VirtualBox Host-Only, VMware Virtual Ethernet); a VM's own network card does not, so
    // Akari-Dash running inside a VM still sees it.
    // Windows' hidden pseudo-adapters (WAN Miniports, the kernel debugger's) also report as Ethernet;
    // they have no real hardware address, and their names give them away too.
    private static readonly string[] VirtualMarkers =
        ["Virtual", "VPN", "TAP-", "Wintun", "WireGuard", "Bluetooth", "Npcap", "WAN Miniport", "Kernel Debug", "Teredo", "IP-HTTPS", "6to4"];

    /// <summary>Every wired and Wi-Fi adapter on this PC, connected or not, without virtual ones.</summary>
    public static IReadOnlyList<NetworkAdapter> Physical()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.NetworkInterfaceType is NetworkInterfaceType.Ethernet
                    or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT
                    or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.Wireless80211)
                .Where(adapter => !VirtualMarkers.Any(marker => adapter.Description.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                .Where(adapter => adapter.GetPhysicalAddress().GetAddressBytes() is { Length: 6 } mac && mac.Any(b => b != 0))
                .Select(adapter => new NetworkAdapter(adapter.Id, adapter.Name, adapter.Description))
                .OrderBy(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (NetworkInformationException)
        {
            return [];
        }
    }
}
