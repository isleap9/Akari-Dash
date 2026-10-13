using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public sealed class SystemCleanupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DiskUsageTests." + Guid.NewGuid().ToString("N"));

    public SystemCleanupTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Games", "Big"));
        Directory.CreateDirectory(Path.Combine(_root, "Docs"));
        File.WriteAllBytes(Path.Combine(_root, "Games", "Big", "pak.bin"), new byte[5000]);
        File.WriteAllBytes(Path.Combine(_root, "Games", "save.dat"), new byte[1000]);
        File.WriteAllBytes(Path.Combine(_root, "Docs", "note.txt"), new byte[10]);
        File.WriteAllBytes(Path.Combine(_root, "loose.log"), new byte[100]);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_scan_adds_up_every_file_and_each_top_level_folder()
    {
        var scan = DiskUsage.Scan(_root);

        Assert.Equal(6110, scan.Bytes);
        Assert.Equal(4, scan.Files);
        Assert.Equal(["Games", "Docs"], scan.TopFolders.Select(folder => folder.Name));
        Assert.Equal(6000, scan.TopFolders[0].Bytes);
        Assert.Equal(2, scan.TopFolders[0].Files);
    }

    [Fact]
    public void Largest_files_come_biggest_first()
    {
        var scan = DiskUsage.Scan(_root);

        Assert.Equal(["pak.bin", "save.dat", "loose.log", "note.txt"], scan.LargestFiles.Select(file => file.Name));
    }

    [Fact]
    public void A_cancelled_scan_stops()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        Assert.Throws<OperationCanceledException>(() => DiskUsage.Scan(_root, cancellationToken: cancel.Token));
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(5_872_026, "5.6 MB")]
    [InlineData(148_384_576_716, "138.2 GB")]
    public void Sizes_read_as_explorer_shows_them(long bytes, string text)
    {
        Assert.Equal(text, DiskUsage.Format(bytes).Replace(',', '.'));
    }

    [Theory]
    [InlineData("Display", true)]
    [InlineData("Net", true)]
    [InlineData("HIDClass", false)]
    [InlineData("AudioProcessingObject", false)]
    public void Deleting_a_driver_a_pc_cannot_do_without_warns_twice(string className, bool critical)
    {
        Assert.Equal(critical, new DriverPackage("oem0.inf", "nvmdi.inf", null, className, "NVIDIA", "1.0", null).IsCritical);
    }

    [Fact]
    public void A_driver_without_a_device_goes_by_its_published_name()
    {
        Assert.Equal("oem22.inf", new DriverPackage("oem22.inf", "x.inf", null, "Extension", "p", "1", null).DisplayName);
        Assert.Equal("Sonar APO", new DriverPackage("oem21.inf", "x.inf", "Sonar APO", "Extension", "p", "1", null).DisplayName);
    }
}
