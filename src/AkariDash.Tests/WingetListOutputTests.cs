using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public class WingetListOutputTests
{
    private static InstallableApp App(string id) =>
        new(id, id, "Publisher", "Description.", InstallableAppCategory.Tools, "#0078D4");

    private static readonly InstallableApp[] Catalogue =
        [App("Valve.Steam"), App("GOG.Galaxy")];

    [Fact]
    public void A_null_output_means_we_could_not_ask() =>
        Assert.Null(WingetListOutput.InstalledIds(null, Catalogue));

    [Fact]
    public void An_id_in_the_output_is_an_installed_app()
    {
        const string output = """
            Name                 Id            Version
            ------------------------------------------
            Steam                Valve.Steam   1.0.0
            """;

        var installed = WingetListOutput.InstalledIds(output, Catalogue);

        Assert.NotNull(installed);
        Assert.Equal(["Valve.Steam"], installed);
    }

    [Fact]
    public void A_longer_id_that_merely_starts_the_same_is_not_the_same_app()
    {
        const string output = """
            Name                 Id                 Version
            -------------------------------------------------
            Steam Link           Valve.SteamLink    1.0.0
            """;

        var installed = WingetListOutput.InstalledIds(output, Catalogue);

        Assert.NotNull(installed);
        Assert.Empty(installed);
    }

    [Fact]
    public void An_id_with_a_plus_is_matched_literally()
    {
        var catalogue = new[] { App("Microsoft.VCRedist.2015+.x64") };
        const string output = """
            Name                                                  Id                              Version
            --------------------------------------------------------------------------------------------
            Microsoft Visual C++ 2015-2022 Redistributable (x64)  Microsoft.VCRedist.2015+.x64    14.40.0
            """;

        var installed = WingetListOutput.InstalledIds(output, catalogue);

        Assert.NotNull(installed);
        Assert.Contains("Microsoft.VCRedist.2015+.x64", installed);
    }

    // A narrow console makes winget truncate the Id column, and a truncated id cannot be told
    // apart from another that shares its prefix - so we do not claim to know what is missing.
    [Fact]
    public void A_truncated_output_says_we_do_not_know() =>
        Assert.Null(WingetListOutput.InstalledIds(
            """
            Name                                                  Id                            Version
            --------------------------------------------------------------------------------------------
            Microsoft Visual C++ 2015-2022 Redistributable (x64)  Microsoft.VCRedist.2015+.x…   14.40.0
            """,
            [App("Microsoft.VCRedist.2015+.x64")]));

    // ...but winget truncates every column, not just the Id, and a truncated name (or the id of
    // some package we do not offer) cannot change the answer about ours.
    [Fact]
    public void A_truncated_token_that_is_not_one_of_ours_does_not_stop_the_answer()
    {
        const string output = """
            Name                                                  Id                    Version
            -----------------------------------------------------------------------------------------
            Steam                                                 Valve.Steam           1.0.0
            Some Very Long Unrelated Application Name That Runs O…
            AnotherUnrelated.Package.Name.Goes.On.For.Ever…
            """;

        var installed = WingetListOutput.InstalledIds(output, Catalogue);

        Assert.NotNull(installed);
        Assert.Equal(["Valve.Steam"], installed);
    }

    // A truncated id that could be one of ours is the one that must never be guessed at.
    [Fact]
    public void A_truncated_id_that_could_be_ours_still_says_we_do_not_know() =>
        Assert.Null(WingetListOutput.InstalledIds(
            """
            Name                                                  Id                    Version
            -----------------------------------------------------------------------------------------
            Steam                                                 Valve.Stea…           1.0.0
            """,
            Catalogue));
}
