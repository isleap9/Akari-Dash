using System.Xml.Linq;
using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class DriverCleanTests
{
    // A clean touches only the card the person named: never every GPU, never the sound drivers.
    [Fact]
    public void DDU_cleans_only_the_vendor_it_is_given()
    {
        Assert.All(Enum.GetValues<GpuVendor>(), vendor =>
        {
            var arguments = DriverClean.Arguments(vendor);

            Assert.Single(arguments);
            Assert.Contains(vendor.ToString(), arguments[0], StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(arguments, argument => argument.Contains("All", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(arguments, argument => argument.Contains("Realtek", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(arguments, argument => argument.Contains("SoundBlaster", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(arguments, argument => argument.Contains("Restart", StringComparison.OrdinalIgnoreCase));
        });
    }

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
    public void DDU_comes_over_https() =>
        Assert.Equal("https", DriverClean.DduDownload.Scheme);

    [Fact]
    public void The_reset_state_survives_the_trip_through_its_file()
    {
        var state = new DriverResetState(
            GpuVendor.Amd, Manual: false, "C:\\ProgramData\\Akari-Dash\\driver-reset\\ddu",
            new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero), DriverResetStage.Cleaned, ExitCode: 0);

        Assert.Equal(state, DriverResetState.FromJson(state.ToJson()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"Vendor":"Nvidia"}""")]
    [InlineData("""{"Vendor":"Matrox","Manual":false,"DduFolder":"C:\\x","Scheduled":"2026-10-15T12:00:00+00:00","Stage":"Cleaned"}""")]
    public void Anything_else_in_the_file_is_no_state(string? json) =>
        Assert.Null(DriverResetState.FromJson(json));

    [Fact]
    public void A_clean_that_never_finished_says_so() =>
        Assert.Contains("did not finish",
            new DriverResetState(GpuVendor.Nvidia, false, "C:\\x", DateTimeOffset.Now, DriverResetStage.Cleaning).Summary);
}
