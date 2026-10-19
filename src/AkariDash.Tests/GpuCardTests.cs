using AkariDash.Core.Gpus;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class GpuCardTests
{
    private static GpuAdapterRaw Raw(
        string matchingDeviceId,
        string name,
        string? driverVersion = null,
        string? driverDate = null,
        object? qwMemorySize = null,
        object? memorySize = null,
        uint? linkSpeed = null,
        uint? linkWidth = null,
        uint? maxLinkSpeed = null,
        uint? maxLinkWidth = null) =>
        new(@"PCI\VEN_10DE&DEV_2786\4&1A2B3C&0&0008", matchingDeviceId, name, driverVersion, driverDate,
            qwMemorySize, memorySize, linkSpeed, linkWidth, maxLinkSpeed, maxLinkWidth)
        {
            DriverKey = @"{4d36e968-e325-11ce-bfc1-08002be10318}\0000",
            ProviderName = "NVIDIA",
        };

    [Fact]
    public void An_nvidia_card_reads_as_a_discrete_card_with_nvidias_own_driver_number()
    {
        var card = GpuCard.From(Raw("pci\\ven_10de&dev_2786", "NVIDIA GeForce RTX 4070", "32.0.15.6094", "6-10-2024",
            qwMemorySize: 12884901888L, linkSpeed: 4, linkWidth: 16, maxLinkSpeed: 4, maxLinkWidth: 16))!;

        Assert.Equal("NVIDIA GeForce RTX 4070", card.Name);
        Assert.Equal(GpuVendor.Nvidia, card.Vendor);
        Assert.Equal(GpuKind.Discrete, card.Kind);
        Assert.Equal("560.94 (32.0.15.6094)", card.DriverVersion);
        Assert.Equal(new DateOnly(2024, 6, 10), card.DriverDate);
        Assert.Equal("12 GB", card.VideoMemory);
        Assert.Equal("PCIe 4.0 x16", card.PcieLink);
        Assert.Equal(@"PCI\VEN_10DE&DEV_2786\4&1A2B3C&0&0008", card.InstanceId);
    }

    [Fact]
    public void The_header_facts_read_as_vain_toolbox_shows_them()
    {
        var card = GpuCard.From(Raw("pci\\ven_10de&dev_2786", "NVIDIA GeForce RTX 4070", "32.0.15.6094"))!;

        Assert.Equal("10DE:2786", card.HardwareId);
        Assert.Equal("0000", card.RegistryKey);
        Assert.Equal("NVIDIA", card.Manufacturer);
        Assert.True(card.HasDriver);
    }

    [Fact]
    public void An_adapter_without_a_driver_version_reads_as_no_driver()
    {
        Assert.False(GpuCard.From(Raw("pci\\ven_10de&dev_2786", "NVIDIA GeForce RTX 4070"))!.HasDriver);
    }

    [Fact]
    public void A_link_running_below_what_the_card_supports_says_both()
    {
        // Cards drop the link to save power when idle, and a card in the wrong slot runs narrower.
        var card = GpuCard.From(Raw("pci\\ven_1002&dev_747e", "AMD Radeon RX 7800 XT",
            linkSpeed: 1, linkWidth: 8, maxLinkSpeed: 4, maxLinkWidth: 16))!;

        Assert.Equal("PCIe 1.0 x8 (supports PCIe 4.0 x16)", card.PcieLink);
    }

    [Fact]
    public void Facts_windows_does_not_report_stay_unknown()
    {
        var card = GpuCard.From(Raw("pci\\ven_8086&dev_a780", "Intel(R) UHD Graphics 770"))!;

        Assert.Null(card.DriverDate);
        Assert.Null(card.VideoMemory);
        Assert.Null(card.PcieLink);
        Assert.Null(card.DriverVersion);
    }

    [Fact]
    public void Older_drivers_report_video_memory_as_four_bytes_or_a_dword()
    {
        Assert.Null(GpuCard.From(Raw("pci\\ven_1002&dev_73ff", "AMD Radeon RX 6600", memorySize: new byte[] { 0, 0, 0, 0 }))!.VideoMemory);
        Assert.Equal("2 GB", GpuCard.From(Raw("pci\\ven_1002&dev_73ff", "AMD Radeon RX 6400", memorySize: BitConverter.GetBytes(2147483648u)))!.VideoMemory);
        Assert.Equal("512 MB", GpuCard.From(Raw("pci\\ven_8086&dev_a780", "Intel(R) UHD Graphics 770", memorySize: 536870912))!.VideoMemory);
    }

    [Theory]
    [InlineData("pci\\ven_8086&dev_a780", "Intel(R) UHD Graphics 770", GpuKind.Integrated)]
    [InlineData("pci\\ven_8086&dev_56a0", "Intel(R) Arc(TM) A770 Graphics", GpuKind.Discrete)]
    [InlineData("pci\\ven_1002&dev_164e", "AMD Radeon(TM) Graphics", GpuKind.Integrated)]
    [InlineData("pci\\ven_1002&dev_15bf", "AMD Radeon 780M Graphics", GpuKind.Integrated)]
    [InlineData("pci\\ven_1002&dev_747e", "AMD Radeon RX 7800 XT", GpuKind.Discrete)]
    [InlineData("pci\\ven_10de&dev_2786", "NVIDIA GeForce RTX 4070", GpuKind.Discrete)]
    public void Integrated_or_discrete_is_told_by_maker_and_name(string matchingDeviceId, string name, GpuKind kind)
    {
        Assert.Equal(kind, GpuCard.From(Raw(matchingDeviceId, name))!.Kind);
    }

    [Fact]
    public void An_adapter_that_is_not_nvidia_amd_or_intel_is_not_a_card_here()
    {
        Assert.Null(GpuCard.From(Raw("pci\\ven_80ee&dev_beef", "VirtualBox Graphics Adapter (WDDM)")));
        Assert.Null(GpuCard.From(Raw("", "Microsoft Basic Display Adapter")));
    }
}
