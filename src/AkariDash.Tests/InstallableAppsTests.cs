using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public class InstallableAppsTests
{
    [Fact]
    public void Every_app_is_listed_once()
    {
        Assert.Equal(
            InstallableApps.All.Count,
            InstallableApps.All.Select(app => app.WingetId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_app_is_complete()
    {
        Assert.All(InstallableApps.All, app =>
        {
            Assert.False(string.IsNullOrWhiteSpace(app.WingetId));
            Assert.False(string.IsNullOrWhiteSpace(app.Name));
            Assert.False(string.IsNullOrWhiteSpace(app.Publisher));
            Assert.False(string.IsNullOrWhiteSpace(app.Description));
            // A winget id is Publisher.Package: at least one dot, no spaces, no leading or trailing dot.
            Assert.Matches(@"^[^\s.]+\.[^\s.]+(\.[^\s.]+)*$", app.WingetId);
            Assert.Matches(@"^#[0-9A-Fa-f]{6}$", app.AccentColor);
        });
    }

    [Fact]
    public void Every_category_has_something_in_it()
    {
        Assert.All(Enum.GetValues<InstallableAppCategory>(), category =>
            Assert.Contains(InstallableApps.All, app => app.Category == category));
    }
}
