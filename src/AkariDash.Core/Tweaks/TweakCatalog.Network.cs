using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

// The Network section of the Gaming page: settings Windows keeps per network adapter, under
// Tcpip(6)\Parameters\Interfaces\{adapter GUID}. Their targets are this PC's wired and Wi-Fi adapters,
// read when Akari-Dash starts (as Match RAM reads the RAM), so they stay Declared Tweaks: Live State,
// the preview, Original Values and Undo work as for any other Tweak. Windows reads these values when
// an adapter starts, so they take effect after a restart.
public static partial class TweakCatalog
{
    private const string Ipv4Interfaces = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
    private const string Ipv6Interfaces = @"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces";

    private static IEnumerable<DeclaredTweak> NetworkTweaks()
    {
        var adapters = NetworkAdapters.Physical();
        return adapters.Count == 0 ? [] : [DnsServers(adapters), NagleAlgorithm(adapters)];
    }

    /// <summary>
    /// The DNS servers of every adapter in <paramref name="adapters"/>, IPv4 and IPv6. The addresses
    /// are each provider's published resolvers; Automatic (an empty NameServer, which is also what a
    /// missing one means) uses the servers the router hands out.
    /// </summary>
    internal static DeclaredTweak DnsServers(IReadOnlyList<NetworkAdapter> adapters)
    {
        var ipv4 = adapters.Select(adapter => NameServer(Ipv4Interfaces, adapter)).ToList();
        var ipv6 = adapters.Select(adapter => NameServer(Ipv6Interfaces, adapter)).ToList();

        return new DeclaredTweak(
            Id: "gaming.dns-servers",
            Title: "DNS servers",
            Description: "Which servers turn website and game server names into addresses, for IPv4 and IPv6. Automatic uses your router's (usually your internet provider's). "
                + $"Applies to: {string.Join(", ", adapters.Select(adapter => adapter.Name))}.",
            Category: Category.Gaming,
            Group: "Network",
            Targets: [.. ipv4, .. ipv6],
            Options:
            [
                Servers("automatic", "Automatic", "", ""),
                Servers("cloudflare", "Cloudflare (1.1.1.1)", "1.1.1.1,1.0.0.1", "2606:4700:4700::1111,2606:4700:4700::1001"),
                Servers("google", "Google (8.8.8.8)", "8.8.8.8,8.8.4.4", "2001:4860:4860::8888,2001:4860:4860::8844"),
                Servers("quad9", "Quad9 (9.9.9.9)", "9.9.9.9,149.112.112.112", "2620:fe::fe,2620:fe::9"),
            ],
            Activation: Activation.AfterRestart);

        TweakOption Servers(string id, string label, string v4, string v6) => new(
            id,
            label,
            ipv4.Select(target => (target, value: v4)).Concat(ipv6.Select(target => (target, value: v6)))
                .ToDictionary(pair => pair.target, pair => (MachineValue?)RegistryValue.String(pair.value)));

        static TweakTarget NameServer(string interfaces, NetworkAdapter adapter) => new(
            new RegistryLocation(RegistryHive.LocalMachine, $@"{interfaces}\{adapter.Id}", "NameServer"),
            AbsentMeans: RegistryValue.String(""));
    }

    /// <summary>
    /// Nagle's algorithm and delayed ACKs on every adapter in <paramref name="adapters"/>: Off sets
    /// TcpAckFrequency and TCPNoDelay to 1 (Microsoft documents TcpAckFrequency per interface; Winhance
    /// sets both). Deleting them is Windows' default.
    /// </summary>
    internal static DeclaredTweak NagleAlgorithm(IReadOnlyList<NetworkAdapter> adapters)
    {
        TweakTarget[] targets =
        [
            .. adapters.SelectMany(adapter => new[] { "TcpAckFrequency", "TCPNoDelay" }
                .Select(name => new TweakTarget(new RegistryLocation(RegistryHive.LocalMachine, $@"{Ipv4Interfaces}\{adapter.Id}", name)))),
        ];

        return new DeclaredTweak(
            Id: "gaming.nagle",
            Title: "Nagle's algorithm",
            Description: "Windows briefly holds back small network packets to send them together, and waits before acknowledging what it receives. Off sends them at once, which can lower latency in some online games. "
                + $"Applies to: {string.Join(", ", adapters.Select(adapter => adapter.Name))}.",
            Category: Category.Gaming,
            Group: "Network",
            Targets: targets,
            Options:
            [
                Option(targets, "on", "On (Windows default)", null),
                DWordOption(targets, "off", "Off", 1),
            ],
            Activation: Activation.AfterRestart);
    }
}
