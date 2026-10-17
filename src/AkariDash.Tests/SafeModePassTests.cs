using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

/// <summary>The pass exchange is a queue now: two safe-mode Tweaks can be pending at once, and the
/// same Tweak re-staged before the restart replaces its own entry.</summary>
public class SafeModePassTests
{
    private static string SomePath() => Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.json");

    [Fact]
    public void Passes_round_trip_as_a_list()
    {
        var path = SomePath();
        SafeModePass.WriteAll(path, [
            new SafeModePass("security.defender-optimize", "optimize", Undo: false),
            new SafeModePass("security.disable-defender", "off", Undo: false),
        ]);

        Assert.Equal(
            ["security.defender-optimize", "security.disable-defender"],
            SafeModePass.ReadList(path).Select(pass => pass.TweakId));

        File.Delete(path);
    }

    // Files written by the previous single-pass app must still read: one record is a one-item list.
    [Fact]
    public void A_legacy_single_record_file_reads_as_one_entry()
    {
        var path = SomePath();
        new SafeModePass("security.defender-optimize", "optimize", Undo: false).Write(path);

        var passes = SafeModePass.ReadList(path);

        var pass = Assert.Single(passes);
        Assert.Equal(new SafeModePass("security.defender-optimize", "optimize", Undo: false), pass);

        File.Delete(path);
    }

    [Fact]
    public void Appending_replaces_the_same_tweaks_entry_in_place_and_keeps_the_others()
    {
        var path = SomePath();
        SafeModePass.Append(path, new SafeModePass("a", "off", Undo: false));
        SafeModePass.Append(path, new SafeModePass("b", "off", Undo: false));
        SafeModePass.Append(path, new SafeModePass("a", "on", Undo: false));

        var passes = SafeModePass.ReadList(path);

        Assert.Equal(2, passes.Count);
        Assert.Equal("b", passes[0].TweakId);
        Assert.Equal(("a", "on"), (passes[1].TweakId, passes[1].OptionId));

        File.Delete(path);
    }

    [Fact]
    public void RemoveTweak_takes_only_its_own_entries_and_deletes_an_empty_file()
    {
        var path = SomePath();
        SafeModePass.Append(path, new SafeModePass("a", "off", Undo: false));
        SafeModePass.Append(path, new SafeModePass("b", "off", Undo: false));

        SafeModePass.RemoveTweak(path, "a");
        Assert.Equal(["b"], SafeModePass.ReadList(path).Select(pass => pass.TweakId));

        SafeModePass.RemoveTweak(path, "a"); // no entry for it: no harm
        SafeModePass.RemoveTweak(path, "b");
        Assert.Empty(SafeModePass.ReadList(path));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void A_missing_file_reads_as_nothing()
    {
        Assert.Empty(SafeModePass.ReadList(SomePath()));
    }
}
