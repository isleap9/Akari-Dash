using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public class InstalledAppTextTests
{
    [Theory]
    [InlineData("Paint", "Microsoft.Paint", "Paint")]
    [InlineData("  Windows Terminal ", "Microsoft.WindowsTerminal", "Windows Terminal")]
    [InlineData("", "Microsoft.WindowsCalculator", "WindowsCalculator")]
    [InlineData(null, "Microsoft.WindowsNotepad", "WindowsNotepad")]
    [InlineData("ms-resource:AppName", "Microsoft.ScreenSketch", "ScreenSketch")]
    [InlineData(null, "NoPublisherPrefix", "NoPublisherPrefix")]
    public void A_package_shows_its_display_name_or_its_package_name_without_the_publisher(string? display, string package, string expected)
    {
        Assert.Equal(expected, InstalledAppText.PackageName(display, package));
    }

    [Theory]
    [InlineData(@"C:\Program Files\App\app.exe", @"C:\Program Files\App\app.exe")]
    [InlineData(@"""C:\Program Files\App\app.exe"",0", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Program Files\App\app.exe,-101", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Program Files\Oracle\VirtualBox Guest Additions\uninst.exe", @"C:\Program Files\Oracle\VirtualBox Guest Additions\uninst.exe")]
    [InlineData(@"C:\Program Files\A, B\app.ico", @"C:\Program Files\A, B\app.ico")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData(@""""",0", null)]
    public void A_display_icon_names_the_file_without_quotes_or_icon_index(string? displayIcon, string? expected)
    {
        Assert.Equal(expected, InstalledAppText.IconPath(displayIcon));
    }
}
