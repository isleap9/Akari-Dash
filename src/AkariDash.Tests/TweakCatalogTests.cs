using AkariDash.Core.Tweaks;
using Xunit;

namespace AkariDash.Tests;

public class TweakCatalogTests
{
    public static TheoryData<string> TweakIds => new(TweakCatalog.All.Select(tweak => tweak.Id));

    private static DeclaredTweak Find(string id) => TweakCatalog.All.Single(tweak => tweak.Id == id);

    [Theory]
    [MemberData(nameof(TweakIds))]
    public void Every_option_gives_a_value_for_every_target(string id)
    {
        var tweak = Find(id);

        Assert.All(tweak.Options, option => Assert.All(tweak.Targets, target => Assert.True(option.Values.ContainsKey(target))));
    }

    [Theory]
    [MemberData(nameof(TweakIds))]
    public void Recommended_option_is_one_of_the_tweaks_own_options(string id)
    {
        var tweak = Find(id);

        if (tweak.Recommended is { } recommended)
        {
            Assert.Contains(recommended, tweak.Options);
        }
    }

    [Fact]
    public void Tweak_ids_are_unique()
    {
        Assert.Equal(TweakCatalog.All.Count, TweakCatalog.All.Select(tweak => tweak.Id).Distinct().Count());
    }

    [Fact]
    public void Processor_scheduling_offers_every_quantum_length_type_and_boost_combination()
    {
        var tweak = Find("gaming.processor-scheduling");
        var target = Assert.Single(tweak.Targets);

        // Each of the three 2-bit fields takes Default/first/second (0-2), as in AkariOS-Ultimate's tuner.
        var values = tweak.Options.Select(option => (int)option.Values[target]!.Data).ToList();
        Assert.Equal(27, values.Distinct().Count());
        Assert.All(values, value => Assert.All(new[] { value >> 4, (value >> 2) & 3, value & 3 }, field => Assert.InRange(field, 0, 2)));
    }

    [Fact]
    public void Processor_scheduling_labels_options_like_the_tuner_and_recommends_0x26()
    {
        var tweak = Find("gaming.processor-scheduling");

        Assert.Equal("Default · Default · High (0x02)", tweak.Options.Single(option => option.Id == "0x02").Label);
        Assert.Equal("Short · Variable · High (0x26)", tweak.Recommended?.Label);
        Assert.Equal("Long · Fixed · None (0x18)", tweak.Options.Single(option => option.Id == "0x18").Label);
    }

    [Fact]
    public void Game_mode_recommends_on()
    {
        Assert.Equal("on", Find("gaming.game-mode").Recommended?.Id);
    }
}
