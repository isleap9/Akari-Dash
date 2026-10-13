using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public class AppxDeprovisioningTests
{
    [Fact]
    public void The_key_is_named_after_the_package_family()
    {
        Assert.Equal(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Deprovisioned\Microsoft.BingNews_8wekyb3d8bbwe",
            AppxDeprovisioning.DeprovisionedKey("Microsoft.BingNews_8wekyb3d8bbwe"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(@"..\..\Run")]
    public void Anything_that_is_not_a_family_name_is_refused(string name)
    {
        // The key is written under HKLM; a blank name would mark the Deprovisioned key itself.
        Assert.Throws<ArgumentException>(() => AppxDeprovisioning.DeprovisionedKey(name));
    }
}
