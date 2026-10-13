using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Xunit;

namespace AkariDash.Tests;

public class WindowsCatalogTests
{
    private static readonly IReadOnlyList<DeclaredTweak> Windows =
        TweakCatalog.All.Where(tweak => tweak.Category == Category.Windows).ToList();

    [Fact]
    public void Windows_page_keeps_its_seven_rows()
    {
        // The five that are not Customize, and AkariOS-Ultimate's black wallpaper and account pictures.
        Assert.Equal(7, Windows.Count);
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
        // The black theme and the black wallpaper are the exceptions: the accent values and the desktop
        // wallpaper always exist, so those rows read from them. A target with no AbsentMeans counts when
        // the Option deletes it.
        Assert.All(Windows.Where(tweak => tweak.Id is not ("windows.black-theme" or "windows.black-sign-out-wallpaper")), tweak =>
            Assert.Contains(tweak.Options, option =>
                tweak.Targets.All(target => Equals(option.Values[target], target.AbsentMeans))));
    }
}
