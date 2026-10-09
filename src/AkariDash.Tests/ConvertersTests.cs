using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using AkariDash.Framework.Converters;
using Xunit;

namespace AkariDash.Tests;

public class ConvertersTests
{
    private static object? Convert(Microsoft.UI.Xaml.Data.IValueConverter converter, object? value, object? parameter = null)
        => converter.Convert(value, typeof(object), parameter, "en-US");

    [Fact]
    public void BooleanToVisibility_converts_true_to_visible()
    {
        var converter = new BooleanToVisibilityConverter();
        Assert.Equal(Visibility.Visible, Convert(converter, true));
        Assert.Equal(Visibility.Collapsed, Convert(converter, false));
    }

    [Fact]
    public void BooleanToVisibility_convert_back()
    {
        var converter = new BooleanToVisibilityConverter();
        Assert.True((bool)converter.ConvertBack(Visibility.Visible, typeof(bool), null, "en-US")!);
        Assert.False((bool)converter.ConvertBack(Visibility.Collapsed, typeof(bool), null, "en-US")!);
    }

    [Fact]
    public void InvertedBooleanToVisibility_inverts()
    {
        var converter = new InvertedBooleanToVisibilityConverter();
        Assert.Equal(Visibility.Collapsed, Convert(converter, true));
        Assert.Equal(Visibility.Visible, Convert(converter, false));
        Assert.False((bool)converter.ConvertBack(Visibility.Visible, typeof(bool), null, "en-US")!);
    }

    [Fact]
    public void InvertedBoolean_negates()
    {
        var converter = new InvertedBooleanConverter();
        Assert.False((bool)Convert(converter, true)!);
        Assert.True((bool)Convert(converter, false)!);
        Assert.False((bool)converter.ConvertBack(true, typeof(bool), null, "en-US")!);
    }

    [Fact]
    public void BoolToValue_maps_true_and_false()
    {
        var converter = new BoolToValueConverter { TrueValue = "Yes", FalseValue = "No" };
        Assert.Equal("Yes", Convert(converter, true));
        Assert.Equal("No", Convert(converter, false));
        Assert.True((bool)converter.ConvertBack("Yes", typeof(bool), null, "en-US")!);
        Assert.False((bool)converter.ConvertBack("No", typeof(bool), null, "en-US")!);
    }

    [Fact]
    public void NullToVisibility_collapses_null()
    {
        var converter = new NullToVisibilityConverter();
        Assert.Equal(Visibility.Collapsed, Convert(converter, null));
        Assert.Equal(Visibility.Visible, Convert(converter, "x"));
    }

    [Fact]
    public void NullToVisibility_invert_flips_behavior()
    {
        var converter = new NullToVisibilityConverter { Invert = true };
        Assert.Equal(Visibility.Visible, Convert(converter, null));
        Assert.Equal(Visibility.Collapsed, Convert(converter, "x"));
    }

    [Fact]
    public void ObjectToVisibility_visible_when_not_null()
    {
        var converter = new ObjectToVisibilityConverter();
        Assert.Equal(Visibility.Collapsed, Convert(converter, null));
        Assert.Equal(Visibility.Visible, Convert(converter, new object()));
    }

    [Fact]
    public void CountToVisibility_uses_minimum_parameter()
    {
        var converter = new CountToVisibilityConverter();
        Assert.Equal(Visibility.Collapsed, Convert(converter, 0));
        Assert.Equal(Visibility.Visible, Convert(converter, 1));
        Assert.Equal(Visibility.Collapsed, Convert(converter, 1, "2"));
        Assert.Equal(Visibility.Visible, Convert(converter, 2, "2"));
    }

    [Fact]
    public void StringToVisibility_trims_whitespace_by_default()
    {
        var converter = new StringToVisibilityConverter();
        Assert.Equal(Visibility.Collapsed, Convert(converter, null));
        Assert.Equal(Visibility.Collapsed, Convert(converter, ""));
        Assert.Equal(Visibility.Collapsed, Convert(converter, "   "));
        Assert.Equal(Visibility.Visible, Convert(converter, "value"));
    }

    [Fact]
    public void StringToVisibility_without_trim_keeps_whitespace()
    {
        var converter = new StringToVisibilityConverter { TrimWhitespace = false };
        Assert.Equal(Visibility.Visible, Convert(converter, "   "));
    }

    [Fact]
    public void CollectionToVisibility_checks_any_items()
    {
        var converter = new CollectionToVisibilityConverter();
        Assert.Equal(Visibility.Collapsed, Convert(converter, new List<int>()));
        Assert.Equal(Visibility.Visible, Convert(converter, new List<int> { 1 }));
        Assert.Equal(Visibility.Collapsed, Convert(converter, null));
    }

    [Fact]
    public void CollectionToVisibility_invert_shows_when_empty()
    {
        var converter = new CollectionToVisibilityConverter { Invert = true };
        Assert.Equal(Visibility.Visible, Convert(converter, new List<int>()));
    }

    [Fact]
    public void DateToString_formats_offsets()
    {
        var converter = new DateToStringConverter();
        var date = new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal("2024-05-01", Convert(converter, date, "yyyy-MM-dd"));
    }

    [Fact]
    public void DateToString_formats_plain_dates()
    {
        var converter = new DateToStringConverter();
        var date = new DateTime(2024, 5, 1, 0, 0, 0);
        Assert.Equal("2024-05-01", Convert(converter, date, "yyyy-MM-dd"));
    }

    private enum TestEnum
    {
        First = 0,

        [System.ComponentModel.Description("The second one")]
        Second = 1,
    }

    [Fact]
    public void EnumToBoolean_matches_parameter()
    {
        var converter = new EnumToBooleanConverter();
        Assert.True((bool)Convert(converter, TestEnum.First, "First")!);
        Assert.False((bool)Convert(converter, TestEnum.First, "Second")!);
        Assert.False((bool)Convert(converter, null, "First")!);
        Assert.Equal(TestEnum.Second, converter.ConvertBack(true, typeof(TestEnum), "Second", "en-US"));

        // The unchecked / invalid cases return DependencyProperty.UnsetValue so a
        // two-way binding does not write back (returning null would be coerced to
        // the enum's default value). UnsetValue is a WinRT static that cannot be
        // resolved in the unit-test host, so reaching it throws COMException here.
        Assert.Throws<COMException>(() => converter.ConvertBack(false, typeof(TestEnum), "Second", "en-US"));
        Assert.Throws<COMException>(() => converter.ConvertBack(true, typeof(TestEnum), "Nope", "en-US"));
    }

    [Fact]
    public void EnumToString_uses_description()
    {
        var converter = new EnumToStringConverter();
        Assert.Equal("First", Convert(converter, TestEnum.First));
        Assert.Equal("The second one", Convert(converter, TestEnum.Second));
        Assert.Equal(string.Empty, Convert(converter, null));
        Assert.Equal("not-an-enum", Convert(converter, "not-an-enum"));
    }

    [Fact]
    public void Passthrough_returns_value_unchanged()
    {
        var converter = new PassthroughConverter();
        var value = new object();
        Assert.Same(value, Convert(converter, value));
        Assert.Same(value, converter.ConvertBack(value, typeof(object), null, "en-US"));
    }

    [Fact]
    public void MathConverter_adds_numbers()
    {
        var converter = new MathConverter();
        Assert.Equal(5d, Convert(converter, new object[] { 2, 3 }, "+"));
    }

    [Fact]
    public void MathConverter_concatenates_strings()
    {
        var converter = new MathConverter();
        Assert.Equal("ab", Convert(converter, new object[] { "a", "b" }, "+"));
    }

    [Theory]
    [InlineData("-", 7, 2, 5d)]
    [InlineData("*", 3, 4, 12d)]
    [InlineData("%", 7, 3, 1d)]
    [InlineData("=", 5, 5, true)]
    [InlineData("!=", 5, 6, true)]
    public void MathConverter_supported_operators(string op, double a, double b, object expected)
    {
        var converter = new MathConverter();
        Assert.Equal(expected, Convert(converter, new object[] { a, b }, op));
    }

    [Fact]
    public void MathConverter_guards_invalid_input()
    {
        var converter = new MathConverter();
        Assert.Null(Convert(converter, new object[] { 1, 0 }, "/"));
        Assert.Null(Convert(converter, new object[] { 1 }, "+"));
        Assert.Null(Convert(converter, 42, "+"));
        Assert.Null(Convert(converter, new object[] { 1, 2 }, "??"));
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack(1, typeof(object), "+", "en-US"));
    }
}
