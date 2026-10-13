using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class BlackThemeTests
{
    private static readonly DeclaredTweak Black = TweakCatalog.All.Single(tweak => tweak.Id == "windows.black-theme");
    private static readonly DeclaredTweak ColorMode = TweakCatalog.All.Single(tweak => tweak.Id == "windows.color-mode");

    private static TweakOption Option(string id) => Black.Options.Single(option => option.Id == id);

    private static MachineValue? Value(TweakOption option, string name, RegistryHive hive = RegistryHive.CurrentUser) =>
        option.Values[Black.Targets.Single(target => target.Location is RegistryLocation location && location.Name == name && location.Hive == hive)];

    [Fact]
    public void It_sits_beside_color_mode_on_the_windows_page_with_no_recommended_option()
    {
        Assert.Equal(Category.Windows, Black.Category);
        Assert.Equal("Appearance", Black.Group);
        Assert.Null(Black.Recommended);
        Assert.Equal(Activation.AfterSignOut, Black.Activation);
        Assert.Equal(["default", "black"], Black.Options.Select(option => option.Id));
    }

    [Fact]
    public void It_shares_color_modes_two_values_rather_than_duplicating_them()
    {
        // The same targets, not look-alikes: the engine recognises a shared value by equality, so a
        // casing difference in the key would make the two rows fight instead of reading as Custom.
        Assert.Equal(ColorMode.Targets, Black.Targets.Take(2));
    }

    [Fact]
    public void Black_writes_what_akarios_ultimate_writes()
    {
        var black = Option("black");

        Assert.Equal(RegistryValue.DWord(0), Value(black, "AppsUseLightTheme"));
        Assert.Equal(RegistryValue.DWord(0), Value(black, "SystemUsesLightTheme"));
        Assert.Equal(RegistryValue.DWord(0), Value(black, "AppsUseLightTheme", RegistryHive.LocalMachine));
        Assert.Equal(RegistryValue.DWord(1), Value(black, "ColorPrevalence"));
        Assert.Equal(RegistryValue.DWord(0), Value(black, "EnableTransparency"));
        Assert.Equal(RegistryValue.DWord(unchecked((int)0xff191919)), Value(black, "AccentColor"));
        Assert.Equal(RegistryValue.DWord(unchecked((int)0xc4191919)), Value(black, "ColorizationColor"));
        Assert.Equal(RegistryValue.DWord(unchecked((int)0xc4191919)), Value(black, "ColorizationAfterglow"));
        Assert.Equal(RegistryValue.DWord(1), Value(black, "EnableWindowColorization"));

        var palette = Assert.IsType<byte[]>(((RegistryValue)Value(black, "AccentPalette")!).Data);
        Assert.Equal(32, palette.Length);
    }

    [Fact]
    public void Windows_default_is_the_sources_own_revert_and_deletes_the_machine_value()
    {
        var revert = Option("default");

        Assert.Equal(RegistryValue.DWord(1), Value(revert, "AppsUseLightTheme"));
        Assert.Equal(RegistryValue.DWord(1), Value(revert, "EnableTransparency"));
        Assert.Equal(RegistryValue.DWord(unchecked((int)0xffd47800)), Value(revert, "AccentColor"));
        Assert.Null(Value(revert, "AppsUseLightTheme", RegistryHive.LocalMachine));
        Assert.Equal(32, ((byte[])((RegistryValue)Value(revert, "AccentPalette")!).Data).Length);
    }

    [Fact]
    public void Every_option_covers_every_target()
    {
        Assert.All(Black.Options, option => Assert.Equal(Black.Targets.Count, option.Values.Count));
    }

    [Fact]
    public void The_desktop_background_colour_is_left_out_because_both_directions_write_the_same()
    {
        Assert.DoesNotContain(Black.Targets, target => target.Location is RegistryLocation { Key: @"Control Panel\Colors" });
    }
}
