using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public class WindowsFeaturesTests
{
    // What dism /English /Online /Get-Capabilities /Format:Table prints, trimmed.
    private const string Table = """

        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1

        Image Version: 10.0.26300.9457

        Capability listing:

        ------------------------------------------------------- | -------------
        Capability Identity                                     | State
        ------------------------------------------------------- | -------------
        App.StepsRecorder~~~~0.0.1.0                            | Installed
        Browser.InternetExplorer~~~~0.0.11.0                    | Not Present
        Microsoft.Windows.Notepad.System~~~~0.0.1.0             | Installed
        WMIC~~~~                                                | Installed

        The operation completed successfully.
        """;

    [Fact]
    public void Reads_each_capability_once_without_the_header_row()
    {
        var capabilities = DismOutput.Capabilities(Table);

        Assert.Equal(
            ["App.StepsRecorder", "Browser.InternetExplorer", "Microsoft.Windows.Notepad.System", "WMIC"],
            capabilities.Select(capability => capability.DisplayName));
        Assert.Equal([true, false, true, true], capabilities.Select(capability => capability.IsOn));
        Assert.All(capabilities, capability => Assert.Equal(WindowsFeatureKind.Capability, capability.Kind));
        Assert.Equal("App.StepsRecorder~~~~0.0.1.0", capabilities[0].Name);
    }

    [Theory]
    [InlineData("Microsoft.Windows.Notepad.System~~~~0.0.1.0", WindowsFeatureKind.Capability)]
    [InlineData("WMIC~~~~", WindowsFeatureKind.Capability)]
    [InlineData("VBSCRIPT~~~~", WindowsFeatureKind.Capability)]
    [InlineData("NetFx3", WindowsFeatureKind.Feature)]
    [InlineData("NetFx4-AdvSrvs", WindowsFeatureKind.Feature)]
    [InlineData("Windows-Defender-Default-Definitions", WindowsFeatureKind.Feature)]
    [InlineData("SearchEngine-Client-Package", WindowsFeatureKind.Feature)]
    public void AkariOS_Ultimates_keep_list_survives_remove_all(string name, WindowsFeatureKind kind)
    {
        var feature = new WindowsFeature(name, name, kind, IsOn: true);

        Assert.NotNull(WindowsFeatureKeepList.KeptBecause(feature));
        Assert.False(WindowsFeatureKeepList.RemovedByRemoveAll(feature));
    }

    [Theory]
    [InlineData("App.StepsRecorder~~~~0.0.1.0", WindowsFeatureKind.Capability)]
    [InlineData("MediaPlayback", WindowsFeatureKind.Feature)]
    [InlineData("Printing-XPSServices-Features", WindowsFeatureKind.Feature)]
    public void Everything_else_that_is_on_goes(string name, WindowsFeatureKind kind)
    {
        Assert.True(WindowsFeatureKeepList.RemovedByRemoveAll(new WindowsFeature(name, name, kind, IsOn: true)));
        Assert.False(WindowsFeatureKeepList.RemovedByRemoveAll(new WindowsFeature(name, name, kind, IsOn: false)));
    }

    [Fact]
    public void A_name_on_the_other_list_is_not_kept()
    {
        // The two keep-lists are separate: "Notepad" keeps a capability, not a Windows feature.
        Assert.Null(WindowsFeatureKeepList.KeptBecause(new WindowsFeature("Notepad-Feature", "x", WindowsFeatureKind.Feature, true)));
    }
}
