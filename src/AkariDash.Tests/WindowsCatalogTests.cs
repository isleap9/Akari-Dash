using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Xunit;

namespace AkariDash.Tests;

public class WindowsCatalogTests
{
    private static readonly IReadOnlyList<DeclaredTweak> Windows =
        TweakCatalog.All.Where(tweak => tweak.Category == Category.Windows).ToList();

    private static DeclaredTweak Find(string id) => Windows.Single(tweak => tweak.Id == id);

    [Fact]
    public void Windows_page_has_about_twenty_tweaks_in_its_groups()
    {
        Assert.InRange(Windows.Count, 18, 25);
        Assert.All(Windows, tweak => Assert.StartsWith("windows.", tweak.Id));
        Assert.Equal(
            ["Appearance", "File Explorer", "Notifications", "Start", "System", "Taskbar"],
            Windows.Select(tweak => tweak.Group).Distinct().Order());
    }

    [Fact]
    public void Windows_tweaks_are_a_matter_of_taste_so_none_has_a_recommended_option()
    {
        Assert.All(Windows, tweak => Assert.Null(tweak.Recommended));
    }

    [Fact]
    public void Missing_values_count_as_one_of_the_options_except_for_policies()
    {
        // A fresh Windows has most of these values missing; it should still read as an Option, not Custom.
        Assert.All(Windows.Where(tweak => tweak.Id != "windows.widgets"), tweak =>
            Assert.Contains(tweak.Options, option =>
                tweak.Targets.All(target => target.AbsentMeans is { } absent && Equals(option.Values[target], absent))));
    }

    [Fact]
    public void Taskbar_search_has_four_options_with_the_search_box_as_windows_default()
    {
        var tweak = Find("windows.taskbar-search");

        Assert.Equal(["hidden", "icon", "icon-label", "box"], tweak.Options.Select(option => option.Id));
        Assert.Equal(RegistryValue.DWord(2), Assert.Single(tweak.Targets).AbsentMeans);
    }

    [Fact]
    public void Classic_context_menu_is_left_out_because_undo_cannot_remove_its_key()
    {
        Assert.DoesNotContain(
            TweakCatalog.All.SelectMany(tweak => tweak.Targets),
            target => target.Location is RegistryLocation { Key: var key } && key.Contains("86ca1aa0-34aa-4e8b-a509-50c905bae2a2"));
    }
}
