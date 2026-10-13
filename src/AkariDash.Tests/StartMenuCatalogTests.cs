using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class StartMenuCatalogTests
{
    private static readonly IReadOnlyList<DeclaredTweak> StartMenu =
        TweakCatalog.All.Where(tweak => tweak.Category == Category.StartMenu).ToList();

    private static DeclaredTweak Find(string id) => StartMenu.Single(tweak => tweak.Id == id);

    private static RegistryLocation Location(DeclaredTweak tweak) =>
        (RegistryLocation)Assert.Single(tweak.Targets).Location;

    [Fact]
    public void Start_menu_page_holds_only_registry_tweaks_in_its_two_groups()
    {
        Assert.Equal(13, StartMenu.Count);
        Assert.All(StartMenu, tweak =>
        {
            Assert.StartsWith("start.", tweak.Id);
            Assert.All(tweak.Targets, target => Assert.IsType<RegistryLocation>(target.Location));
        });
        Assert.Equal(
            ["Layout", "What Start shows"],
            StartMenu.Select(tweak => tweak.Group).Distinct());
    }

    [Fact]
    public void How_start_looks_is_a_matter_of_taste_so_none_has_a_recommended_option()
    {
        Assert.All(StartMenu, tweak => Assert.Null(tweak.Recommended));
    }

    [Fact]
    public void Every_row_is_one_current_user_value_that_windows_reads_at_sign_in()
    {
        Assert.All(StartMenu, tweak =>
        {
            var location = Location(tweak);
            Assert.Equal(RegistryHive.CurrentUser, location.Hive);
            Assert.Equal(Activation.AfterSignOut, tweak.Activation);
        });
    }

    [Fact]
    public void Every_row_offers_exactly_two_options()
    {
        // Start is a list of yes-or-no choices; the two that are not write a value or delete it.
        Assert.All(StartMenu, tweak => Assert.Equal(2, tweak.Options.Count));
    }

    [Fact]
    public void A_missing_value_reads_as_an_option_rather_than_custom()
    {
        // A fresh Windows has all of these missing, and the page must still show which way Start is
        // set rather than Custom on every row. The two rows whose other values are undocumented say
        // so by deleting instead, so absent is that Option and needs no AbsentMeans.
        foreach (var tweak in StartMenu)
        {
            var target = Assert.Single(tweak.Targets);
            var deletes = tweak.Options.Any(option => option.Values[target] is null);

            Assert.True(
                deletes || tweak.Options.Any(option => Equals(option.Values[target], target.AbsentMeans)),
                $"{tweak.Id} reads as Custom on a fresh Windows");
        }
    }

    [Theory]
    [InlineData("start.pinned-section", "ShowPinnedSection")]
    [InlineData("start.recommended-section", "ShowRecentSection")]
    [InlineData("start.all-apps-section", "ShowAllAppsSection")]
    [InlineData("start.all-pins", "ShowAllPinsList")]
    [InlineData("start.size", "StartMenuSize")]
    [InlineData("start.all-apps-view", "AllAppsViewMode")]
    [InlineData("start.user-information", "HideUserInformation")]
    [InlineData("start.recently-added", "ShowRecentList")]
    [InlineData("start.most-used", "ShowFrequentList")]
    [InlineData("start.suggested-files", "ShowSuggestedFiles")]
    public void The_settings_start_page_values_live_in_the_start_key(string id, string name)
    {
        var location = Location(Find(id));

        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Start", location.Key);
        Assert.Equal(name, location.Name);
    }

    [Theory]
    [InlineData("start.recommendations", "Start_IrisRecommendations")]
    [InlineData("start.browsing-history", "Start_RecoPersonalizedSites")]
    [InlineData("start.account-notifications", "Start_AccountNotifications")]
    public void The_older_start_settings_live_in_explorer_advanced(string id, string name)
    {
        var location = Location(Find(id));

        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", location.Key);
        Assert.Equal(name, location.Name);
    }

    [Theory]
    [InlineData("start.recently-added")]
    [InlineData("start.most-used")]
    [InlineData("start.recommendations")]
    [InlineData("start.account-notifications")]
    public void The_four_rows_that_moved_off_the_windows_page_kept_their_original_values(string id)
    {
        // Their records are filed under the windows. id on a PC that applied them before the move.
        var old = RenamedTweaks.Ids.Single(pair => pair.Value == id).Key;

        Assert.StartsWith("windows.start-", old);
        Assert.Contains(StartMenu, tweak => tweak.Id == id);
        Assert.DoesNotContain(TweakCatalog.All, tweak => tweak.Id == old);
    }

    [Fact]
    public void Hiding_the_recommended_section_uses_the_start_menus_own_value_not_the_education_policy()
    {
        // Winhance hides it with the Education-only policy plus IsEducationEnvironment, which tells
        // Windows this is a school PC. ShowRecentSection does the same job and claims nothing.
        Assert.Equal(RegistryValue.DWord(0), Find("start.recommended-section").Options.Single(option => option.Id == "hide").Values.Values.Single());
        Assert.DoesNotContain(
            TweakCatalog.All.SelectMany(tweak => tweak.Targets),
            target => target.Location is RegistryLocation { Name: "IsEducationEnvironment" or "HideRecommendedSection" });
    }

    [Fact]
    public void The_start_layout_row_is_left_out_because_current_windows_removed_the_value()
    {
        // Winhance caps Start_Layout at build 26120; past that it writes a value nothing reads.
        Assert.DoesNotContain(
            TweakCatalog.All.SelectMany(tweak => tweak.Targets),
            target => target.Location is RegistryLocation { Name: "Start_Layout" });
    }

    [Theory]
    [InlineData("start.size")]
    [InlineData("start.all-apps-view")]
    public void A_row_whose_other_values_are_undocumented_hands_the_choice_back_by_deleting(string id)
    {
        var tweak = Find(id);
        var target = Assert.Single(tweak.Targets);

        Assert.Null(target.AbsentMeans);
        Assert.Null(tweak.Options.Last().Values[target]);
        Assert.Equal("windows-default", tweak.Options.Last().Id);
    }

    [Fact]
    public void Nothing_else_in_the_catalog_writes_a_value_this_page_owns()
    {
        // Two owners of one value would fight: each reads the other's apply as drift of its own.
        var mine = StartMenu.SelectMany(tweak => tweak.Targets).Select(target => target.Location).ToHashSet();
        var others = TweakCatalog.All
            .Where(tweak => tweak.Category != Category.StartMenu)
            .SelectMany(tweak => tweak.Targets.Select(target => (tweak.Id, target.Location)));

        Assert.DoesNotContain(others, other => mine.Contains(other.Location));
    }

    [Fact]
    public void Recently_opened_items_stays_on_the_privacy_page()
    {
        // Start_TrackDocs reaches jump lists and File Explorer too, so Start is not where it belongs.
        var tweak = TweakCatalog.All.Single(tweak => tweak.Id == "privacy.recent-items");

        Assert.Equal(Category.Privacy, tweak.Category);
        Assert.Equal("Start_TrackDocs", ((RegistryLocation)Assert.Single(tweak.Targets).Location).Name);
    }
}
