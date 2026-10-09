using AkariDash.Framework;
using Xunit;

namespace AkariDash.Tests;

public class ObjectExtensionsTests
{
    [Fact]
    public void Let_applies_function()
    {
        var result = 5.Let(x => x * 2);

        Assert.Equal(10, result);
    }

    [Fact]
    public void Let_propagates_value()
    {
        var input = new object();
        object? output = null;

        var result = input.Let(x => output = x);

        Assert.Same(input, result);
        Assert.Same(input, output);
    }

    [Fact]
    public void IfNotNull_invokes_action_for_non_null()
    {
        var invoked = false;
        var value = new object();

        var result = value.IfNotNull(_ => invoked = true);

        Assert.Same(value, result);
        Assert.True(invoked);
    }

    [Fact]
    public void IfNotNull_skips_action_for_null()
    {
        var invoked = false;

        object? value = null;
        var result = value.IfNotNull(_ => invoked = true);

        Assert.Null(result);
        Assert.False(invoked);
    }
}
