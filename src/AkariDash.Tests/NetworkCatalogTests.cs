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
}
