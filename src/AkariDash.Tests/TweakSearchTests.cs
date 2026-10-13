using AkariDash.Core.Tweaks;
using Xunit;

namespace AkariDash.Tests;

/// <summary>The shell's search box: finding a Tweak by what it is called or by what it writes.</summary>
public class TweakSearchTests
{
    private static IReadOnlyList<string> Ids(string query, int limit = 12) =>
        TweakSearch.Find(query, limit: limit).Select(match => match.Tweak.Id).ToList();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void An_empty_query_finds_nothing(string? query)
    {
        Assert.Empty(TweakSearch.Find(query));
    }

    [Fact]
    public void A_title_that_starts_with_the_query_comes_first()
    {
        Assert.Equal("gaming.game-mode", Ids("game mode")[0]);
        Assert.Equal("privacy.cortana", Ids("cortana")[0]);
    }

    // The word someone remembers is rarely the first one in the title.
    [Theory]
    [InlineData("hover", "gaming.mouse-hover-time")]
    [InlineData("spotlight", "privacy.windows-spotlight")]
    [InlineData("slideshow", "privacy.lock-screen-slideshow")]
    public void A_word_inside_the_title_finds_it(string query, string id)
    {
        Assert.Equal(id, Ids(query)[0]);
    }

    // A word two Tweaks share finds both, the one whose title has it ranked by Category then title:
    // "sense" is Storage Sense as much as Wi-Fi Sense.
    [Fact]
    public void A_word_two_tweaks_share_finds_both()
    {
        var found = Ids("sense");

        Assert.Equal(["gaming.storage-sense", "privacy.wifi-sense"], found);
    }

    // What Windows calls the value, which is what someone following a guide has to go on.
    [Theory]
    [InlineData("SilentInstalledAppsEnabled", "privacy.automatic-app-installs")]
    [InlineData("Win32PrioritySeparation", "gaming.processor-scheduling")]
    [InlineData("AllowTelemetry", "privacy.diagnostic-data")]
    [InlineData("SysMain", "services.sysmain")]
    public void The_name_of_the_value_a_tweak_writes_finds_it(string query, string id)
    {
        Assert.Contains(id, Ids(query));
    }

    [Fact]
    public void The_value_that_matched_is_named_so_the_result_can_be_told_apart()
    {
        // The suggestions preset writes this value as well, so both are found; the Tweak that is
        // only about it comes first.
        var found = TweakSearch.Find("SilentInstalledAppsEnabled");

        Assert.Equal(
            ["privacy.automatic-app-installs", "privacy.suggestions-preset"],
            found.Select(match => match.Tweak.Id));
        Assert.All(found, match =>
        {
            Assert.Contains("SilentInstalledAppsEnabled", match.Detail);
            Assert.Contains("Privacy", match.Detail);
        });
    }

    [Fact]
    public void A_result_says_which_category_and_group_it_is_in()
    {
        var match = TweakSearch.Find("game mode")[0];

        Assert.Equal("Gaming > Game Mode", match.Detail);
    }

    [Fact]
    public void A_group_name_finds_everything_in_it()
    {
        var found = Ids("lock screen", limit: 40);

        Assert.Contains("privacy.windows-spotlight", found);
        Assert.Contains("privacy.lock-screen-tips", found);
        Assert.Contains("privacy.lock-screen-slideshow", found);
    }

    [Fact]
    public void A_category_name_finds_that_pages_tweaks_after_any_title_that_says_it()
    {
        var found = Ids("services", limit: 40);

        // Titles first: Narrator online services says it outright.
        Assert.Equal("privacy.narrator-online-services", found[0]);
        Assert.True(found.Count(id => id.StartsWith("services.")) > 20, "most results should be the Services page");
    }

    // Several words, each somewhere in the Tweak, in any order.
    [Theory]
    [InlineData("xbox saves", "services.xbox-game-saves")]
    [InlineData("narrator scripting", "privacy.narrator-scripting")]
    [InlineData("tips lock screen", "privacy.lock-screen-tips")]
    public void Every_word_somewhere_in_the_tweak_finds_it(string query, string id)
    {
        Assert.Contains(id, Ids(query, limit: 40));
    }

    [Fact]
    public void Case_does_not_matter()
    {
        Assert.Equal(Ids("CORTANA"), Ids("cortana"));
        Assert.Equal(Ids("SysMain"), Ids("sysmain"));
    }

    [Fact]
    public void An_id_finds_its_own_tweak()
    {
        Assert.Contains("privacy.suggestions-preset", Ids("privacy.suggestions-preset"));
    }

    [Fact]
    public void A_query_that_matches_nothing_finds_nothing()
    {
        Assert.Empty(TweakSearch.Find("zzzqqxnotathing"));
    }

    [Fact]
    public void No_more_than_the_limit_comes_back()
    {
        Assert.Equal(3, TweakSearch.Find("windows", limit: 3).Count);
    }

    [Fact]
    public void Every_tweak_in_the_catalog_can_be_found_by_its_own_title()
    {
        foreach (var tweak in TweakCatalog.All)
        {
            var found = TweakSearch.Find(tweak.Title, limit: 60);

            Assert.Contains(tweak.Id, found.Select(match => match.Tweak.Id));
        }
    }
}
