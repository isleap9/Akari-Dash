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
        Assert.Equal(RegistryValue.DWord(1), Value(black, "EnableWindowColorization"));
        Assert.Equal(RegistryValue.DWord(0), Value(black, "AccentColorMenu"));

        var palette = Assert.IsType<byte[]>(((RegistryValue)Value(black, "AccentPalette")!).Data);
        Assert.Equal(32, palette.Length);
    }

    [Fact]
    public void Windows_default_is_the_sources_own_revert_and_deletes_the_machine_value()
    {
        var revert = Option("default");

        Assert.Equal(RegistryValue.DWord(1), Value(revert, "AppsUseLightTheme"));
        Assert.Equal(RegistryValue.DWord(1), Value(revert, "EnableTransparency"));
        Assert.Equal(RegistryValue.DWord(unchecked((int)0xffd47800)), Value(revert, "AccentColorMenu"));
        Assert.Null(Value(revert, "AppsUseLightTheme", RegistryHive.LocalMachine));
        Assert.Equal(32, ((byte[])((RegistryValue)Value(revert, "AccentPalette")!).Data).Length);
    }

    [Fact]
    public void Every_option_covers_every_target()
    {
        Assert.All(Black.Options, option => Assert.Equal(Black.Targets.Count, option.Values.Count));
    }

    [Theory]
    [InlineData("AccentColor")]
    [InlineData("ColorizationColor")]
    [InlineData("ColorizationAfterglow")]
    public void The_dwm_colours_windows_derives_for_itself_are_not_targets(string name)
    {
        Assert.DoesNotContain(Black.Targets, target => target.Location is RegistryLocation { Key: @"Software\Microsoft\Windows\DWM" } location && location.Name == name);
    }

    [Fact]
    public void A_pc_that_applied_black_before_the_fix_reads_black_again_and_undo_restores_all_twelve()
    {
        // The VM, 2026-10-13: Black applied by the twelve-target build, then a sign-out, after which
        // Windows rewrote the three DWM colours from the palette and the row read Drift.
        string[] derived = ["AccentColor", "ColorizationColor", "ColorizationAfterglow"];
        var machine = new InMemoryMachine();
        var store = new InMemoryOriginalValuesStore();

        // What the PC had before: an accent colour of its own in all three.
        RegistryLocation Dwm(string name) => new(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\DWM", name);
        foreach (var name in derived)
        {
            machine.Write(Dwm(name), RegistryValue.DWord(unchecked((int)0xff8a2be2)));
        }

        var engine = new TweakEngine(machine, store);
        engine.Apply(Black, Option("black"));

        // The old build wrote the three as well, so its record holds their originals.
        var record = store.Get(Black.Id)!;
        store.Save(Black.Id, record with
        {
            OriginalValues = [.. record.OriginalValues, .. derived.Select(name => new OriginalValue(Dwm(name), RegistryValue.DWord(unchecked((int)0xff8a2be2))))],
        });

        // Sign-out: Windows derives black from the palette.
        machine.Write(Dwm("AccentColor"), RegistryValue.DWord(0));
        machine.Write(Dwm("ColorizationColor"), RegistryValue.DWord(unchecked((int)0xc4000000)));
        machine.Write(Dwm("ColorizationAfterglow"), RegistryValue.DWord(unchecked((int)0xc4000000)));

        Assert.Equal("black", Assert.IsType<LiveState.InOption>(engine.ReadLiveState(Black, TweakCatalog.All)).Option.Id);

        engine.Undo(Black);
        Assert.All(derived, name => Assert.Equal(RegistryValue.DWord(unchecked((int)0xff8a2be2)), machine.Read(Dwm(name))));
    }

    [Fact]
    public void The_desktop_background_colour_is_left_out_because_both_directions_write_the_same()
    {
        Assert.DoesNotContain(Black.Targets, target => target.Location is RegistryLocation { Key: @"Control Panel\Colors" });
    }
}
