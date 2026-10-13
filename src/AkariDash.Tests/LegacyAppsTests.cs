using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public class LegacyAppsTests
{
    private static string ScriptsFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "AkariDash.slnx")))
        {
            folder = folder.Parent;
        }

        return Path.Combine(folder!.FullName, "src", "AkariDash.App", "Scripts");
    }

    [Fact]
    public void Edge_and_webview_are_told_apart_by_their_exact_uninstall_names()
    {
        // WebView2's entry contains "Microsoft Edge", which is why AkariOS-Ultimate's "*Microsoft Edge*"
        // match took both entries away; one row must never run the other's script.
        Assert.Same(LegacyApps.Edge, LegacyApps.ForUninstallName("Microsoft Edge"));
        Assert.Same(LegacyApps.WebView, LegacyApps.ForUninstallName("Microsoft Edge WebView2 Runtime"));
        Assert.Null(LegacyApps.ForUninstallName("Microsoft Edge Update"));
        Assert.Null(LegacyApps.ForUninstallName("7-Zip 26.03 (x64)"));
    }

    [Theory]
    [InlineData("Microsoft GameInput", "gameinput")]
    [InlineData("Microsoft GameInput 3.5.283", "gameinput")]
    [InlineData("Update for x64-based Windows Systems (KB5001716)", "update-for-windows")]
    [InlineData("Microsoft OneDrive", "onedrive")]
    public void A_prefix_name_matches_every_version_of_the_entry(string displayName, string id)
    {
        Assert.Equal(id, LegacyApps.ForUninstallName(displayName)?.Id);
    }

    [Fact]
    public void Every_script_a_legacy_app_names_ships_in_the_app()
    {
        var scripts = ScriptsFolder();
        Assert.All(LegacyApps.All, app =>
        {
            Assert.True(File.Exists(Path.Combine(scripts, app.RemoveScript)), app.RemoveScript);
            Assert.True(app.InstallScript is null || File.Exists(Path.Combine(scripts, app.InstallScript)), app.InstallScript);
        });
    }

    [Fact]
    public void Ids_are_unique_and_every_app_says_what_removing_it_costs()
    {
        Assert.Equal(LegacyApps.All.Count, LegacyApps.All.Select(app => app.Id).Distinct().Count());
        Assert.All(LegacyApps.All, app => Assert.False(string.IsNullOrWhiteSpace(app.Warning)));
        Assert.Contains("Widgets", LegacyApps.WebView.Warning);
    }
}
