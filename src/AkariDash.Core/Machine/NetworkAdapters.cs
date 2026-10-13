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

    private static readonly NetworkInterfaceType[] Wired =
    [
        NetworkInterfaceType.Ethernet, NetworkInterfaceType.GigabitEthernet,
        NetworkInterfaceType.FastEthernetT, NetworkInterfaceType.FastEthernetFx,
    ];

    /// <summary>One interface as Windows reports it, before Akari-Dash decides whether it is an adapter.</summary>
    internal sealed record Interface(string Id, string Name, string Description, NetworkInterfaceType Type, byte[] Mac);

    /// <summary>Every wired and Wi-Fi adapter on this PC, connected or not, without virtual ones.</summary>
    public static IReadOnlyList<NetworkAdapter> Physical()
    {
        try
        {
            return Choose(NetworkInterface.GetAllNetworkInterfaces().Select(adapter => new Interface(
                adapter.Id, adapter.Name, adapter.Description, adapter.NetworkInterfaceType,
                adapter.GetPhysicalAddress().GetAddressBytes())));
        }
        catch (NetworkInformationException)
        {
            return [];
        }
    }

    /// <summary>
    /// The real adapters among <paramref name="interfaces"/>.
    ///
    /// Windows also reports every filter driver bound to a card as an interface of its own — the QoS
    /// Packet Scheduler and the two WFP LightWeight Filters on every wired card, named
    /// "Ethernet-QoS Packet Scheduler-0000" and so on — with the card's type and the card's hardware
    /// address. They are not IP interfaces: Windows keeps no Tcpip settings for them and deletes what
    /// is written there at the next restart, which showed up in the VM as DNS servers and Nagle's
    /// algorithm drifting on three GUIDs while the card itself held 1.1.1.1. So of the interfaces that
    /// share a hardware address, only the card counts, and the card is the one whose name the filters
    /// extend: the shortest.
    /// </summary>
    internal static IReadOnlyList<NetworkAdapter> Choose(IEnumerable<Interface> interfaces) =>
        interfaces
            .Where(candidate => Wired.Contains(candidate.Type) || candidate.Type == NetworkInterfaceType.Wireless80211)
            .Where(candidate => !VirtualMarkers.Any(marker => candidate.Description.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .Where(candidate => candidate.Mac is { Length: 6 } mac && mac.Any(b => b != 0))
            .GroupBy(candidate => Convert.ToHexString(candidate.Mac))
            .Select(sameCard => sameCard.OrderBy(candidate => candidate.Name.Length).ThenBy(candidate => candidate.Name, StringComparer.Ordinal).First())
            .Select(card => new NetworkAdapter(card.Id, card.Name, card.Description))
            .OrderBy(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
