using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

/// <summary>A registry key's existence as a target, which the classic right-click menu needs.</summary>
public class RegistryKeyLocationTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "RegistryKeyLocationTests." + Guid.NewGuid().ToString("N"));

    private static readonly RegistryKeyLocation Key = new(RegistryHive.CurrentUser, @"Software\Classes\CLSID\{00000000-0000-0000-0000-000000000001}");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch
        {
        }
    }

    [Theory]
    [InlineData("Software")]
    [InlineData(@"Software\Classes")]
    [InlineData(@"\Software\\Classes\")]
    public void A_key_near_the_root_cannot_be_a_target_because_deleting_it_deletes_its_whole_tree(string key)
    {
        Assert.Throws<ArgumentException>(() => new RegistryKeyLocation(RegistryHive.CurrentUser, key));
    }

    [Fact]
    public void Two_locations_for_the_same_key_are_the_same_target()
    {
        Assert.Equal(Key, new RegistryKeyLocation(RegistryHive.CurrentUser, Key.Key));
        Assert.NotEqual(Key, new RegistryKeyLocation(RegistryHive.LocalMachine, Key.Key));
    }

    [Fact]
    public void Both_did_not_exist_and_exists_survive_the_original_values_file()
    {
        var store = new FileOriginalValuesStore(_folder);
        var other = new RegistryKeyLocation(RegistryHive.CurrentUser, Key.Key + "2");
        store.Save("t", new AppliedTweak("on", [new OriginalValue(Key, null), new OriginalValue(other, KeyPresence.Present)]));

        var back = new FileOriginalValuesStore(_folder).Get("t")!;

        Assert.Equal(new OriginalValue(Key, null), back.OriginalValues[0]);
        Assert.Equal(new OriginalValue(other, KeyPresence.Present), back.OriginalValues[1]);
    }

    [Fact]
    public void A_missing_key_is_a_state_not_a_reason_to_be_unavailable()
    {
        // A missing service makes a Tweak Unavailable; a missing key is just "did not exist", which is
        // one of the classic menu's two Options.
        var tweak = new DeclaredTweak("t", "T", "", Category.Explorer, "G", [new TweakTarget(Key)],
            [
                new TweakOption("off", "Off", new Dictionary<TweakTarget, MachineValue?> { [new TweakTarget(Key)] = null }),
                new TweakOption("on", "On", new Dictionary<TweakTarget, MachineValue?> { [new TweakTarget(Key)] = KeyPresence.Present }),
            ]);

        Assert.Equal("off", Assert.IsType<LiveState.InOption>(new TweakEngine(new InMemoryMachine()).ReadLiveState(tweak)).Option.Id);
    }

    [Fact]
    public void Deleting_a_key_takes_the_values_under_it_and_nothing_beside_it()
    {
        var machine = new InMemoryMachine();
        var inside = new RegistryLocation(RegistryHive.CurrentUser, Key.Key + @"\InprocServer32", "");
        var beside = new RegistryLocation(RegistryHive.CurrentUser, Key.Key + "-sibling", "Kept");
        machine.Write(inside, RegistryValue.String(""));
        machine.Write(beside, RegistryValue.DWord(1));

        Assert.Equal(KeyPresence.Present, machine.Read(Key));
        machine.Delete(Key);

        Assert.Null(machine.Read(Key));
        Assert.Null(machine.Read(inside));
        Assert.Equal(RegistryValue.DWord(1), machine.Read(beside));
    }
}
