using AkariDash.Core.Machine;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class RegistryValueTests
{
    [Fact]
    public void Binary_values_with_the_same_bytes_are_equal()
    {
        var a = new RegistryValue(RegistryValueKind.Binary, new byte[] { 1, 2, 3 });
        var b = new RegistryValue(RegistryValueKind.Binary, new byte[] { 1, 2, 3 });

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, new RegistryValue(RegistryValueKind.Binary, new byte[] { 1, 2, 4 }));
    }

    [Fact]
    public void Multi_string_values_with_the_same_strings_are_equal()
    {
        var a = new RegistryValue(RegistryValueKind.MultiString, new[] { "a", "b" });
        var b = new RegistryValue(RegistryValueKind.MultiString, new[] { "a", "b" });

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Values_of_different_kinds_are_not_equal()
    {
        Assert.NotEqual(RegistryValue.DWord(1), new RegistryValue(RegistryValueKind.QWord, 1L));
        Assert.NotEqual(RegistryValue.String("1"), new RegistryValue(RegistryValueKind.ExpandString, "1"));
    }

    [Fact]
    public void Array_values_describe_their_contents()
    {
        Assert.Equal("01 02 FF", new RegistryValue(RegistryValueKind.Binary, new byte[] { 1, 2, 255 }).ToString());
        Assert.Equal("a; b", new RegistryValue(RegistryValueKind.MultiString, new[] { "a", "b" }).ToString());
    }
}
