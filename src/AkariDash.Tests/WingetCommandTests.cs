using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public class WingetCommandTests
{
    private static InstallableApp App(string id) =>
        new(id, "Name", "Publisher", "Description.", InstallableAppCategory.Tools, "#0078D4");

    [Fact]
    public void Install_is_exact_silent_and_agrees_to_everything()
    {
        Assert.Equal(
            new[]
            {
                "install", "--id", "Valve.Steam", "--source", "winget", "--exact", "--silent",
                "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity",
            },
            WingetCommand.Install(App("Valve.Steam")));
    }

    [Fact]
    public void Install_does_not_mangle_an_id_with_a_plus_in_it()
    {
        Assert.Contains(
            "Microsoft.VCRedist.2015+.x64",
            WingetCommand.Install(App("Microsoft.VCRedist.2015+.x64")));
    }

    [Fact]
    public void List_asks_for_no_interaction()
    {
        var arguments = WingetCommand.List();
        Assert.Equal("list", arguments[0]);
        Assert.Contains("--disable-interactivity", arguments);
    }

    [Fact]
    public void Describe_is_the_line_a_person_would_type()
    {
        Assert.Equal(
            "winget install --id Valve.Steam --source winget",
            WingetCommand.Describe(["install", "--id", "Valve.Steam", "--source", "winget"]));
    }
}
