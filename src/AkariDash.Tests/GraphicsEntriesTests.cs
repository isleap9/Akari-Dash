using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class GraphicsEntriesTests
{
    private static string ScriptsFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "AkariDash.slnx")))
        {
            folder = folder.Parent;
        }

        return Path.Combine(folder!.FullName, "src", "AkariDash.App", "Scripts", "Graphics");
    }

    private static IEnumerable<string> Rows(IReadOnlyList<GraphicsEntry> entries) =>
        entries.SelectMany(entry => entry.Actions.Select(action => $"{entry.Id}: {action.Label}"));

    [Fact]
    public void An_nvidia_pc_sees_akarios_graphics_category_with_only_its_own_vendor_rows()
    {
        var shown = GraphicsEntries.For(new HashSet<GpuVendor> { GpuVendor.Nvidia });

        Assert.Equal(
            [
                "driver-clean: DDU: Auto",
                "driver-clean: DDU: Manual",
                "driver-install: NVIDIA",
                "driver-install-settings: NVIDIA",
                "driver-debloat: NVIDIA",
                "nvidia-settings: Apply",
                "nvidia-settings: Revert",
                "hdcp: Apply",
                "hdcp: Revert",
                "p0-state: Apply",
                "p0-state: Revert",
                "msi-mode: Apply",
                "msi-mode: Revert",
                "directx: Install",
                "cpp: Install",
                "resolution: Open",
                "hags-windowed: Open",
            ],
            Rows(shown));
    }

    [Fact]
    public void An_amd_and_intel_pc_sees_both_vendors_and_no_nvidia_only_entry()
    {
        var shown = GraphicsEntries.For(new HashSet<GpuVendor> { GpuVendor.Amd, GpuVendor.Intel });

        Assert.Equal(
            ["driver-clean", "driver-install", "driver-install-settings", "driver-debloat", "amd-settings", "intel-settings", "msi-mode", "directx", "cpp", "resolution", "hags-windowed"],
            shown.Select(entry => entry.Id));
        Assert.Equal(["AMD", "INTEL"], shown.Single(entry => entry.Id == "driver-install").Actions.Select(action => action.Label));
    }

    [Fact]
    public void A_pc_with_no_known_card_still_gets_the_vendor_free_entries()
    {
        var shown = GraphicsEntries.For(new HashSet<GpuVendor>());

        Assert.Equal(["driver-clean", "msi-mode", "directx", "cpp", "resolution", "hags-windowed"], shown.Select(entry => entry.Id));
    }

    [Fact]
    public void Only_the_two_ddu_runs_ask_first_with_akarios_own_words()
    {
        var confirms = GraphicsEntries.All
            .SelectMany(entry => entry.Actions)
            .Where(action => action.Confirm is not null)
            .Select(action => action.Confirm);

        Assert.Equal(
            ["DDU will remove your graphics driver and restart the PC. Continue?", "DDU will remove your graphics driver. Continue?"],
            confirms);
    }

    [Fact]
    public void Every_action_runs_a_script_that_ships_with_the_app_and_every_script_is_used()
    {
        var folder = ScriptsFolder();
        var used = GraphicsEntries.All.SelectMany(entry => entry.Actions).Select(action => action.Script).Distinct().ToList();

        Assert.All(used, script => Assert.True(File.Exists(Path.Combine(folder, script)), script));
        Assert.Equal(
            Directory.GetFiles(folder, "*.ps1").Select(Path.GetFileName).Where(name => name != GraphicsEntries.Helpers).Order(),
            used.Order());
    }

    [Fact]
    public void Both_ddu_runs_prepare_ddu_now_and_clean_in_the_safe_mode_companion()
    {
        var clean = GraphicsEntries.All.Single(entry => entry.Id == "driver-clean").Actions;

        Assert.All(clean, action => Assert.Equal("driver-clean-prepare.ps1", action.Script));
        Assert.Equal([DriverClean.Auto, DriverClean.Manual], clean.Select(action => action.SafeModeOption));
        Assert.All(
            GraphicsEntries.All.Where(entry => entry.Id != "driver-clean").SelectMany(entry => entry.Actions),
            action => Assert.Null(action.SafeModeOption));
    }

    [Fact]
    public void A_ddu_pass_is_queued_like_any_other_safe_mode_pass()
    {
        var pass = DriverClean.Pass(DriverClean.Auto);

        Assert.Equal(new SafeModePass("driver-clean", "auto", Undo: false), pass);
        Assert.True(DriverClean.IsFor(pass));
        Assert.False(DriverClean.IsFor(new SafeModePass("security.disable-defender", "off", Undo: false)));
    }

    [Fact]
    public void Auto_runs_ddu_with_akarios_arguments_and_leaves_the_restart_to_the_companion()
    {
        // AkariOS: -CleanSoundBlaster -CleanRealtek -CleanAllGpus -Restart. The companion restarts
        // itself once the outcome is saved, so DDU's own -Restart is left off.
        Assert.Equal(["-CleanSoundBlaster", "-CleanRealtek", "-CleanAllGpus"], DriverClean.Arguments(DriverClean.Auto));
        Assert.Empty(DriverClean.Arguments(DriverClean.Manual));
        Assert.EndsWith(@"Temp\ddu\Display Driver Uninstaller.exe", DriverClean.Executable);
    }
}
