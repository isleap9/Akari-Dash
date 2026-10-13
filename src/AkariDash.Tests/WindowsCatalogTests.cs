using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Xunit;

namespace AkariDash.Tests;

public class WindowsCatalogTests
{
    private static readonly IReadOnlyList<DeclaredTweak> Windows =
        TweakCatalog.All.Where(tweak => tweak.Category == Category.Windows).ToList();

    [Fact]
    public void Windows_page_keeps_the_five_rows_that_are_not_customize()
    {
        Assert.Equal(5, Windows.Count);
        Assert.All(Windows, tweak => Assert.StartsWith("windows.", tweak.Id));
        // Notifications, Start, Taskbar and File Explorer used to be Groups here; all are Categories now.
        Assert.Equal(
            ["Appearance", "Multitasking", "System"],
            Windows.Select(tweak => tweak.Group).Distinct().Order());
    }

    [Fact]
    public void Windows_tweaks_are_a_matter_of_taste_so_none_has_a_recommended_option()
    {
        Assert.All(Windows, tweak => Assert.Null(tweak.Recommended));
    }

    [Fact]
    public void Missing_values_count_as_one_of_the_options()
    {
        // A fresh Windows has most of these values missing; it should still read as an Option, not Custom.
        // The black theme is the exception: its accent values always exist, so it reads from them.
        Assert.All(Windows.Where(tweak => tweak.Id != "windows.black-theme"), tweak =>
            Assert.Contains(tweak.Options, option =>
                tweak.Targets.All(target => target.AbsentMeans is { } absent && Equals(option.Values[target], absent))));
    }
}
