using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public class StoreAppListsTests
{
    private static StoreApp App(string name, StoreAppKind kind = StoreAppKind.App) =>
        new(name, name, "Microsoft Corporation", "1.0.0.0", "x64", $"{name}_1.0.0.0_x64__8wekyb3d8bbwe", $"{name}_8wekyb3d8bbwe", kind);

    [Theory]
    [InlineData("Microsoft.Windows.Photos")]
    [InlineData("Microsoft.HEVCVideoExtension")]
    [InlineData("Microsoft.SecHealthUI")]
    [InlineData("MicrosoftWindows.Client.CBS")]
    [InlineData("Microsoft.Windows.StartMenuExperienceHost")]
    [InlineData("NVIDIACorp.NVIDIAControlPanel")]
    [InlineData("windows.immersivecontrolpanel")]
    public void AkariOS_Ultimates_keep_list_is_never_removed(string name)
    {
        Assert.NotNull(AppxKeepList.ProtectedBecause(name));
        Assert.False(AppxKeepList.IsBloatware(App(name)));
    }

    [Theory]
    [InlineData("Microsoft.BingNews")]
    [InlineData("Microsoft.GamingApp")]
    [InlineData("Clipchamp.Clipchamp")]
    [InlineData("Microsoft.ZuneMusic")]
    public void Ordinary_apps_are_bloatware(string name)
    {
        Assert.Null(AppxKeepList.ProtectedBecause(name));
        Assert.True(AppxKeepList.IsBloatware(App(name)));
    }

    [Theory]
    [InlineData("Microsoft.WindowsStore")]
    [InlineData("Microsoft.DesktopAppInstaller")]
    public void Remove_all_leaves_what_restoration_installs_through_but_each_can_go_on_its_own(string name)
    {
        Assert.Null(AppxKeepList.ProtectedBecause(name));
        Assert.False(AppxKeepList.IsBloatware(App(name)));
    }

    [Theory]
    [InlineData(StoreAppKind.System)]
    [InlineData(StoreAppKind.Framework)]
    public void Remove_all_only_takes_apps(StoreAppKind kind)
    {
        Assert.False(AppxKeepList.IsBloatware(App("Microsoft.VCLibs.140.00", kind)));
    }

    [Fact]
    public void Every_restorable_app_is_listed_once_with_a_description()
    {
        Assert.Equal(
            RestorableApps.All.Count,
            RestorableApps.All.Select(app => app.PackageName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(RestorableApps.All, app =>
        {
            Assert.False(string.IsNullOrWhiteSpace(app.Name));
            Assert.False(string.IsNullOrWhiteSpace(app.Description));
            Assert.DoesNotContain('_', app.PackageName);
        });
    }

    [Fact]
    public void Nothing_on_the_keep_list_needs_restoring()
    {
        Assert.All(RestorableApps.All, app => Assert.Null(AppxKeepList.ProtectedBecause(app.PackageName)));
    }
}
