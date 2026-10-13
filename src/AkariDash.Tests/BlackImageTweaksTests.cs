using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public sealed class BlackImageTweaksTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "BlackImageTweaksTests." + Guid.NewGuid().ToString("N"));

    private static DeclaredTweak Find(string id) => TweakCatalog.All.Single(tweak => tweak.Id == id);

    private static TweakOption OptionOf(DeclaredTweak tweak, string id) => tweak.Options.Single(option => option.Id == id);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Black_points_the_lock_screen_and_the_desktop_at_the_image_it_draws()
    {
        // AkariOS-Ultimate's signout-wallpaper: one image, used by both, and no blur behind sign-in.
        var tweak = Find("windows.black-sign-out-wallpaper");
        var black = OptionOf(tweak, "black");

        var image = Assert.IsType<BlackImageLocation>(tweak.Targets[0].Location);
        Assert.Equal(@"C:\Windows\Black.jpg", image.Path);
        Assert.Equal(BlackImage.Black, black.Values[tweak.Targets[0]]);
        Assert.Equal(2, black.Values.Values.Count(value => Equals(value, RegistryValue.String(image.Path))));
        Assert.Equal(Activation.AfterSignOut, tweak.Activation);
    }

    [Fact]
    public void Undo_takes_away_a_black_image_that_was_not_there_before()
    {
        var machine = new InMemoryMachine();
        var engine = new TweakEngine(machine);
        var tweak = Find("windows.black-sign-out-wallpaper");
        var wallpaper = tweak.Targets.Single(target => target.Location is RegistryLocation { Name: "Wallpaper" });
        machine.Write(wallpaper.Location, RegistryValue.String(@"D:\Pictures\mine.jpg"));

        engine.Apply(tweak, OptionOf(tweak, "black"));
        Assert.Equal("black", Assert.IsType<LiveState.InOption>(engine.ReadLiveState(tweak)).Option.Id);

        engine.Undo(tweak);
        Assert.Null(machine.Read(tweak.Targets[0].Location));
        Assert.Equal(RegistryValue.String(@"D:\Pictures\mine.jpg"), machine.Read(wallpaper.Location));
    }

    [Fact]
    public void Account_pictures_read_as_windows_default_until_painted_and_again_after_undo()
    {
        var machine = new InMemoryMachine();
        var engine = new TweakEngine(machine);
        var tweak = Find("windows.black-account-pictures");

        Assert.Equal("default", Assert.IsType<LiveState.InOption>(engine.ReadLiveState(tweak)).Option.Id);
        engine.Apply(tweak, OptionOf(tweak, "black"));
        Assert.Equal(BlackImage.Black, machine.Read(AccountPicturesLocation.Default));
        engine.Undo(tweak);
        Assert.Null(machine.Read(AccountPicturesLocation.Default));
    }

    [Fact]
    public void Both_new_targets_survive_the_original_values_file()
    {
        var applied = new AppliedTweak("black",
        [
            new OriginalValue(new BlackImageLocation(@"C:\Windows\Black.jpg"), null),
            new OriginalValue(AccountPicturesLocation.Default, BlackImage.Black),
            new OriginalValue(new RegistryLocation(RegistryHive.CurrentUser, @"Control Panel\Desktop", "Wallpaper"), RegistryValue.String("x")),
        ]);

        new FileOriginalValuesStore(_folder).Save("t", applied);
        var read = new FileOriginalValuesStore(_folder).Get("t")!;

        Assert.Equal(applied.OriginalValues, read.OriginalValues);
    }
}
