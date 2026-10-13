using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public class LegacyAppsTests
{
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

    [Fact]
    public void Each_reinstalls_from_the_akarios_files_release()
    {
        Assert.All(LegacyApps.All, app =>
        {
            Assert.Equal("https", app.Installer.Scheme);
            Assert.StartsWith("/isleap9/AkariOS-Files/releases/download/", app.Installer.AbsolutePath);
        });
    }

    [Fact]
    public void Removing_webview_warns_what_stops_working()
    {
        Assert.Contains("Widgets", LegacyApps.WebView.Warning);
    }
}
