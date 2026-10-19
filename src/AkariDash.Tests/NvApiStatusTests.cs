using AkariDash.Core.Gpus.NvApi;
using Xunit;

namespace AkariDash.Tests;

/// <summary>
/// NvAPI_Status codes read as NVIDIA's own one-line descriptions in nvapi_lite_common.h
/// (github.com/NVIDIA/nvapi, MIT); the expected text is copied from there.
/// </summary>
public class NvApiStatusTests
{
    [Theory]
    [InlineData(-1, "Generic error")]
    [InlineData(-3, "not implemented in current driver installation")]
    [InlineData(-6, "No NVIDIA display driver, or NVIDIA GPU driving a display, was found.")]
    [InlineData(-9, "An argument's structure version is not supported")]
    [InlineData(-10, "The handle is no longer valid (likely due to GPU or display re-configuration)")]
    public void A_status_reads_as_nvidias_description(int status, string expected)
    {
        Assert.Equal(expected, NvApiStatus.Describe(status));
    }

    [Fact]
    public void An_unknown_status_still_says_its_number()
    {
        Assert.Equal("Unknown NVAPI status -9999", NvApiStatus.Describe(-9999));
    }

    [Fact]
    public void A_failed_call_names_the_function_and_the_reason()
    {
        var error = new NvApiException("Disp_ColorControl", -9);

        Assert.Equal(-9, error.Status);
        Assert.Equal("NVIDIA Disp_ColorControl failed: An argument's structure version is not supported (status -9).", error.Message);
    }
}
