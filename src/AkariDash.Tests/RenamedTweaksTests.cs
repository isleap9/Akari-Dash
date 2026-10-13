using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class RenamedTweaksTests
{
    private static readonly OriginalValue Original = new(
        new RegistryLocation(RegistryHive.CurrentUser, @"Software\Test", "Value"),
        RegistryValue.DWord(1));

    private static InMemoryOriginalValuesStore StoreWith(string id, string option)
    {
        var store = new InMemoryOriginalValuesStore();
        store.Save(id, new AppliedTweak(option, [Original]));
        return store;
    }

    [Fact]
    public void Every_old_id_is_gone_from_the_catalog_and_every_new_one_is_in_it()
    {
        var ids = TweakCatalog.All.Select(tweak => tweak.Id).ToHashSet();

        Assert.All(RenamedTweaks.Ids, rename =>
        {
            Assert.DoesNotContain(rename.Key, ids);
            Assert.Contains(rename.Value, ids);
        });
    }

    [Fact]
    public void A_record_under_an_old_id_is_filed_under_the_current_one()
    {
        var store = StoreWith("windows.tips-notifications", "off");

        var moved = RenamedTweaks.Migrate(store);

        Assert.Equal([("windows.tips-notifications", "notifications.tips")], moved);
        Assert.Null(store.Get("windows.tips-notifications"));
        var applied = Assert.IsType<AppliedTweak>(store.Get("notifications.tips"));
        Assert.Equal("off", applied.LastAppliedOptionId);
        Assert.Equal([Original], applied.OriginalValues);
    }

    [Fact]
    public void Migrating_again_changes_nothing()
    {
        var store = StoreWith("windows.tips-notifications", "off");
        RenamedTweaks.Migrate(store);

        Assert.Empty(RenamedTweaks.Migrate(store));
        Assert.Equal(["notifications.tips"], store.AppliedTweakIds());
    }

    [Fact]
    public void A_record_already_under_the_current_id_is_never_overwritten()
    {
        // Both were applied, at different moments, so their Original Values describe different
        // machines: the current record stays, and the old one is left for Home to offer as retired.
        var store = StoreWith("notifications.tips", "on");
        store.Save("windows.tips-notifications", new AppliedTweak("off", []));

        Assert.Empty(RenamedTweaks.Migrate(store));
        Assert.Equal("on", store.Get("notifications.tips")?.LastAppliedOptionId);
        Assert.Equal("off", store.Get("windows.tips-notifications")?.LastAppliedOptionId);
    }

    [Fact]
    public void A_tweak_that_never_moved_is_left_alone()
    {
        var store = StoreWith("privacy.cortana", "blocked");

        Assert.Empty(RenamedTweaks.Migrate(store));
        Assert.Equal(["privacy.cortana"], store.AppliedTweakIds());
    }

    [Fact]
    public void A_migrated_tweak_is_no_longer_retired_and_can_be_undone_from_its_own_row()
    {
        var tweak = TweakCatalog.All.Single(candidate => candidate.Id == "notifications.tips");
        var machine = new InMemoryMachine();
        var store = StoreWith("windows.tips-notifications", "off");

        RenamedTweaks.Migrate(store);
        var engine = new TweakEngine(machine, store);

        Assert.True(engine.IsApplied(tweak));
        Assert.DoesNotContain(engine.RetiredTweaks(TweakCatalog.All), retired => retired.Id.StartsWith("windows."));
    }
}
