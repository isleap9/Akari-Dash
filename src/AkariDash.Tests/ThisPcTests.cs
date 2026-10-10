using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class ThisPcTests
{
    private static readonly PcFacts Typical = new(
        ProductName: "Windows 10 Pro",
        DisplayVersion: "22H2",
        Build: 19045,
        Revision: 5011,
        Cpu: "AMD Ryzen 7 5800X3D 8-Core Processor",
        Gpus: ["NVIDIA GeForce RTX 3080"],
        InstalledRamKilobytes: 32UL * 1024 * 1024,
        RunningAs: @"DESKTOP\alice",
        SignedIn: @"DESKTOP\alice");

    [Fact]
    public void Shows_the_edition_version_and_build()
    {
        var pc = new ThisPc(Typical);

        Assert.Equal("Windows 10 Pro 22H2 (build 19045.5011)", pc.Os);
        Assert.Equal("AMD Ryzen 7 5800X3D 8-Core Processor", pc.Cpu);
        Assert.Equal("NVIDIA GeForce RTX 3080", pc.Gpu);
        Assert.Equal("32 GB", pc.Ram);
    }

    // Windows 11 still reports "Windows 10" as its product name; only the build tells them apart.
    [Fact]
    public void Calls_Windows_11_by_its_name_although_Windows_reports_Windows_10()
    {
        var pc = new ThisPc(Typical with { DisplayVersion = "24H2", Build = 26100, Revision = 2033 });

        Assert.Equal("Windows 11 Pro 24H2 (build 26100.2033)", pc.Os);
    }

    [Fact]
    public void Leaves_out_the_parts_of_the_version_Windows_does_not_report()
    {
        Assert.Equal("Windows 10 Pro (build 19045)", new ThisPc(Typical with { DisplayVersion = null, Revision = null }).Os);
        Assert.Equal("Windows 10 Pro 22H2", new ThisPc(Typical with { Build = null, Revision = null }).Os);
    }

    [Fact]
    public void Values_Windows_cannot_report_are_not_available()
    {
        var pc = new ThisPc(new PcFacts(null, null, null, null, null, [], null, null, null));

        Assert.Null(pc.Os);
        Assert.Null(pc.Cpu);
        Assert.Null(pc.Gpu);
        Assert.Null(pc.Ram);
    }

    [Fact]
    public void Tidies_the_padding_Windows_leaves_in_the_processor_name()
    {
        var pc = new ThisPc(Typical with { Cpu = "  Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz   " });

        Assert.Equal("Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz", pc.Cpu);
        Assert.Null(new ThisPc(Typical with { Cpu = "   " }).Cpu);
    }

    [Fact]
    public void Lists_every_graphics_card_once()
    {
        var pc = new ThisPc(Typical with { Gpus = ["Intel(R) UHD Graphics 630", "NVIDIA GeForce RTX 3080", "Intel(R) UHD Graphics 630"] });

        Assert.Equal("Intel(R) UHD Graphics 630, NVIDIA GeForce RTX 3080", pc.Gpu);
    }

    [Fact]
    public void Shows_memory_that_is_not_a_whole_number_of_gigabytes_to_one_decimal()
    {
        Assert.Equal("15.9 GB", new ThisPc(Typical with { InstalledRamKilobytes = 16_672_000 }).Ram);
    }

    [Fact]
    public void Warns_when_running_as_an_account_other_than_the_signed_in_user()
    {
        var pc = new ThisPc(Typical with { RunningAs = @"DESKTOP\admin", SignedIn = @"DESKTOP\alice" });

        Assert.True(pc.RunsAsOtherAccount);
        Assert.Equal(@"DESKTOP\admin", pc.RunningAs);
        Assert.Equal(@"DESKTOP\alice", pc.SignedIn);
    }

    [Fact]
    public void Does_not_warn_when_running_as_the_signed_in_user()
    {
        Assert.False(new ThisPc(Typical).RunsAsOtherAccount);
        Assert.False(new ThisPc(Typical with { RunningAs = @"desktop\ALICE" }).RunsAsOtherAccount);
    }

    [Fact]
    public void Does_not_warn_when_either_account_is_unknown()
    {
        Assert.False(new ThisPc(Typical with { RunningAs = null }).RunsAsOtherAccount);
        Assert.False(new ThisPc(Typical with { SignedIn = null }).RunsAsOtherAccount);
    }

    [Fact]
    public void Machines_pass_the_facts_through()
    {
        var machine = new InMemoryMachine().WithPc(Typical);

        Assert.Equal(Typical, machine.DescribePc());
        Assert.Equal(Typical, new DryRunMachine(machine).DescribePc());
    }
}
