using System.Xml.Linq;
using AkariDash.Core.Drivers;
using Xunit;

namespace AkariDash.Tests;

public class DriverCleanTests
{
    // AkariOS's automatic clean (Graphics.ps1:84), less -Restart: the companion restarts once it has
    // written down how the clean went.
    [Fact]
    public void DDU_cleans_as_AkariOS_does()
    {
        Assert.Equal(["-CleanSoundBlaster", "-CleanRealtek", "-CleanAllGpus"], DriverClean.Arguments);
        Assert.DoesNotContain(DriverClean.Arguments, argument => argument.Contains("Restart", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Windows_Update_is_kept_from_searching_for_drivers() =>
        Assert.Equal(
            new DriverSetting(DriverSettingRoot.HklmSoftware, @"Microsoft\Windows\CurrentVersion\DriverSearching", "SearchOrderConfig", DriverSettingKind.Dword, "0"),
            DriverClean.SearchOrderConfig);

    // Safe mode skips a RunOnce value unless its name starts with an asterisk.
    [Fact]
    public void The_RunOnce_value_runs_in_safe_mode() =>
        Assert.StartsWith("*", DriverClean.RunOnceValueName);

    [Fact]
    public void The_RunOnce_command_quotes_both_paths() =>
        Assert.Equal(
            "\"C:\\Program Files\\Akari Dash\\AkariDash.DriverReset.exe\" --state \"C:\\ProgramData\\Akari-Dash\\driver-reset.json\"",
            DriverClean.RunOnceCommand(
                "C:\\Program Files\\Akari Dash\\AkariDash.DriverReset.exe",
                "C:\\ProgramData\\Akari-Dash\\driver-reset.json"));

    [Fact]
    public void DDU_settings_are_well_formed_and_keep_DDU_from_handling_safe_mode_itself()
    {
        var settings = XDocument.Parse(DriverClean.DduSettingsXml).Descendants("Settings").Single();

        Assert.Equal("False", settings.Element("EnableSafeModeDialog")?.Value);
        Assert.Equal("False", settings.Element("CheckUpdates")?.Value);
        Assert.Equal("False", settings.Element("ShowOffer")?.Value);
    }

    [Fact]
    public void DDU_and_7Zip_come_over_https()
    {
        Assert.Equal("https", DriverClean.DduDownload.Scheme);
        Assert.Equal("https", SevenZip.Download.Scheme);
    }

    [Fact]
    public void SevenZip_unpacks_the_way_AkariOS_calls_it() =>
        Assert.Equal(["x", "C:\\a\\ddu.exe", "-oC:\\a\\ddu", "-y"], SevenZip.Extract("C:\\a\\ddu.exe", "C:\\a\\ddu"));

    [Fact]
    public void The_reset_state_survives_the_trip_through_its_file()
    {
        var state = new DriverResetState(
            Manual: false, "C:\\ProgramData\\Akari-Dash\\driver-reset\\ddu",
            new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero), DriverResetStage.Cleaned, ExitCode: 0);

        Assert.Equal(state, DriverResetState.FromJson(state.ToJson()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"Manual":false}""")]
    [InlineData("""{"Manual":false,"DduFolder":"C:\\x","Scheduled":"2026-10-15T12:00:00+00:00","Stage":"Exploded"}""")]
    public void Anything_else_in_the_file_is_no_state(string? json) =>
        Assert.Null(DriverResetState.FromJson(json));

    [Fact]
    public void A_clean_that_never_finished_says_so() =>
        Assert.Contains("did not finish",
            new DriverResetState(false, "C:\\x", DateTimeOffset.Now, DriverResetStage.Cleaning).Summary);
}
