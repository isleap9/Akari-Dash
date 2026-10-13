using System.Net.NetworkInformation;
using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Xunit;

namespace AkariDash.Tests;

/// <summary>DNS servers and Nagle's algorithm, set per network adapter (issue #24).</summary>
public class NetworkCatalogTests
{
    private static readonly NetworkAdapter[] Adapters =
    [
        new("{B91EAF22-8169-4C0F-A810-52CF40036EBF}", "Ethernet", "Realtek Gaming 2.5GbE Family Controller"),
        new("{11111111-2222-3333-4444-555555555555}", "Wi-Fi", "Intel Wi-Fi 6E AX211"),
    ];

    private static string Value(TweakOption option, TweakTarget target) => (string)((RegistryValue)option.Values[target]!).Data;

    [Fact]
    public void Dns_targets_are_the_ipv4_and_ipv6_name_servers_of_every_adapter()
    {
        var dns = TweakCatalog.DnsServers(Adapters);
        var keys = dns.Targets.Select(target => ((RegistryLocation)target.Location).Key).ToList();

        Assert.Equal(4, dns.Targets.Count);
        Assert.All(dns.Targets, target => Assert.Equal("NameServer", ((RegistryLocation)target.Location).Name));
        Assert.Contains(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{B91EAF22-8169-4C0F-A810-52CF40036EBF}", keys);
        Assert.Contains(@"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces\{11111111-2222-3333-4444-555555555555}", keys);
        Assert.Contains("Ethernet, Wi-Fi", dns.Description);
    }

    [Fact]
    public void Automatic_is_an_empty_name_server_which_is_also_what_a_missing_one_means()
    {
        var dns = TweakCatalog.DnsServers(Adapters);
        var automatic = dns.Options.Single(option => option.Id == "automatic");

        Assert.All(dns.Targets, target =>
        {
            Assert.Equal("", Value(automatic, target));
            Assert.Equal(RegistryValue.String(""), target.AbsentMeans);
        });
    }

    [Theory]
    [InlineData("cloudflare", "1.1.1.1,1.0.0.1", "2606:4700:4700::1111,2606:4700:4700::1001")]
    [InlineData("google", "8.8.8.8,8.8.4.4", "2001:4860:4860::8888,2001:4860:4860::8844")]
    [InlineData("quad9", "9.9.9.9,149.112.112.112", "2620:fe::fe,2620:fe::9")]
    public void Each_provider_sets_its_ipv4_and_ipv6_servers_on_every_adapter(string id, string v4, string v6)
    {
        var dns = TweakCatalog.DnsServers(Adapters);
        var option = dns.Options.Single(option => option.Id == id);

        Assert.All(dns.Targets, target => Assert.Equal(
            ((RegistryLocation)target.Location).Key.Contains("Tcpip6") ? v6 : v4, Value(option, target)));
    }

    [Fact]
    public void Nagle_off_sets_both_values_on_every_adapter_and_on_deletes_them()
    {
        var nagle = TweakCatalog.NagleAlgorithm(Adapters);
        var off = nagle.Options.Single(option => option.Id == "off");
        var on = nagle.Options.Single(option => option.Id == "on");

        Assert.Equal(4, nagle.Targets.Count);
        Assert.All(nagle.Targets, target =>
        {
            Assert.Equal(RegistryValue.DWord(1), off.Values[target]);
            Assert.Null(on.Values[target]);
        });
    }

    [Fact]
    public void Network_tweaks_take_effect_after_a_restart_and_have_no_recommended_option()
    {
        Assert.All([TweakCatalog.DnsServers(Adapters), TweakCatalog.NagleAlgorithm(Adapters)], tweak =>
        {
            Assert.Equal(Activation.AfterRestart, tweak.Activation);
            Assert.Null(tweak.Recommended);
            Assert.Equal("Network", tweak.Group);
        });
    }

    [Fact]
    public void Host_side_virtual_adapters_are_left_out()
    {
        Assert.DoesNotContain(NetworkAdapters.Physical(), adapter =>
            adapter.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase));
    }

    private static readonly byte[] CardMac = [0x00, 0x15, 0x5D, 0x01, 0x02, 0x03];

    private static NetworkAdapters.Interface Interface(string id, string name, byte[] mac,
        NetworkInterfaceType type = NetworkInterfaceType.Ethernet, string description = "Microsoft Hyper-V Network Adapter") =>
        new(id, name, description, type, mac);

    [Fact]
    public void A_cards_filter_drivers_are_not_adapters_of_their_own()
    {
        // The VM's own list, 2026-10-13: one wired card, and Windows reporting each filter bound to it
        // as another Ethernet interface with the same hardware address. DNS servers and Nagle wrote
        // to all four; Windows deleted the three filter GUIDs' values at restart and both rows read
        // Drift, while the card itself still held 1.1.1.1.
        var adapters = NetworkAdapters.Choose(
        [
            Interface("{081CD06E-C706-11F1-83B2-806E6F6E6963}", "Ethernet", CardMac),
            Interface("{081CD06F-C706-11F1-83B2-806E6F6E6963}", "Ethernet-QoS Packet Scheduler-0000", CardMac),
            Interface("{081CD070-C706-11F1-83B2-806E6F6E6963}", "Ethernet-WFP 802.3 MAC Layer LightWeight Filter-0000", CardMac),
            Interface("{081CD071-C706-11F1-83B2-806E6F6E6963}", "Ethernet-WFP Native MAC Layer LightWeight Filter-0000", CardMac),
        ]);

        var card = Assert.Single(adapters);
        Assert.Equal("Ethernet", card.Name);
        Assert.Equal("{081CD06E-C706-11F1-83B2-806E6F6E6963}", card.Id);
    }

    [Fact]
    public void Two_real_cards_both_stay_and_each_loses_only_its_own_filters()
    {
        byte[] wifiMac = [0xA4, 0xB1, 0xC1, 0x11, 0x22, 0x33];

        var adapters = NetworkAdapters.Choose(
        [
            Interface("{E}", "Ethernet", CardMac),
            Interface("{EQ}", "Ethernet-QoS Packet Scheduler-0000", CardMac),
            Interface("{W}", "Wi-Fi", wifiMac, NetworkInterfaceType.Wireless80211, "Intel Wi-Fi 6E AX211"),
            Interface("{WQ}", "Wi-Fi-QoS Packet Scheduler-0000", wifiMac, NetworkInterfaceType.Wireless80211, "Intel Wi-Fi 6E AX211"),
        ]);

        Assert.Equal(["Ethernet", "Wi-Fi"], adapters.Select(adapter => adapter.Name));
    }

    [Fact]
    public void The_existing_rules_still_hold_after_the_filter_rule()
    {
        var adapters = NetworkAdapters.Choose(
        [
            Interface("{V}", "vEthernet (Default Switch)", [0x00, 0x15, 0x5D, 0xAA, 0xBB, 0xCC], description: "Hyper-V Virtual Ethernet Adapter"),
            Interface("{Z}", "Local Area Connection* 1", [0, 0, 0, 0, 0, 0]),
            Interface("{L}", "Loopback Pseudo-Interface 1", [], NetworkInterfaceType.Loopback),
        ]);

        Assert.Empty(adapters);
    }

    [Fact]
    public void A_pc_that_applied_before_the_fix_reads_its_option_again_and_undo_cleans_up_the_filter_entries()
    {
        // The record from the old build holds Original Values for the card and its three filters.
        // After the fix the row only targets the card, which still holds Akari-Dash's write, so it
        // reads Cloudflare with Undo kept - and Undo still walks the whole record, deleting nothing
        // that exists on the filters, because Windows already took those values away.
        NetworkAdapter card = new("{CARD}", "Ethernet", "Microsoft Hyper-V Network Adapter");
        NetworkAdapter[] withFilters =
        [
            card,
            new("{QOS}", "Ethernet-QoS Packet Scheduler-0000", card.Description),
        ];

        var machine = new InMemoryMachine();
        var store = new InMemoryOriginalValuesStore();
        var oldBuild = TweakCatalog.DnsServers(withFilters);
        var cloudflare = oldBuild.Options.Single(option => option.Id == "cloudflare");
        new TweakEngine(machine, store).Apply(oldBuild, cloudflare);

        // Restart: Windows drops the filter's values.
        foreach (var target in oldBuild.Targets.Where(target => target.Location.ToString().Contains("{QOS}")))
        {
            machine.Delete(target.Location);
        }

        var engine = new TweakEngine(machine, store);
        var fixedRow = TweakCatalog.DnsServers([card]);

        var state = Assert.IsType<LiveState.InOption>(engine.ReadLiveState(fixedRow, [fixedRow]));
        Assert.Equal("cloudflare", state.Option.Id);
        Assert.True(engine.IsApplied(fixedRow));

        engine.Undo(fixedRow);
        Assert.All(oldBuild.Targets, target => Assert.Null(machine.Read(target.Location)));
    }
}
