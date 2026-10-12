using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

/// <summary>Tweaks over single bits of a binary registry value, such as UserPreferencesMask (issue #23).</summary>
public class TweakEngineBitTests
{
    private static readonly RegistryLocation Mask = new(RegistryHive.CurrentUser, @"Control Panel\Desktop", "UserPreferencesMask");
    private static readonly RegistryBitLocation MenuAnimation = new(Mask.Hive, Mask.Key, Mask.Name, 0, 0x02);
    private static readonly RegistryBitLocation CursorShadow = new(Mask.Hive, Mask.Key, Mask.Name, 1, 0x20);

    private static readonly DeclaredTweak Tweak = BitTweak("menu-animation", MenuAnimation);
    private static readonly DeclaredTweak ShadowTweak = BitTweak("cursor-shadow", CursorShadow);

    // Windows 11's own UserPreferencesMask: 9E 1E 07 80 12 00 00 00.
    private readonly InMemoryMachine _machine = new InMemoryMachine()
        .With(Mask, new RegistryValue(RegistryValueKind.Binary, new byte[] { 0x9E, 0x1E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00 }));

    private readonly InMemoryOriginalValuesStore _store = new();

    private static DeclaredTweak BitTweak(string id, RegistryBitLocation bit)
    {
        var target = new TweakTarget(bit);
        return new DeclaredTweak(id, id, "A bit.", Category.Gaming, "Visual effects", [target],
        [
            new TweakOption("off", "Off", new Dictionary<TweakTarget, MachineValue?> { [target] = BitValue.Off }),
            new TweakOption("on", "On", new Dictionary<TweakTarget, MachineValue?> { [target] = BitValue.On }),
        ]);
    }

    private TweakEngine Engine() => new(_machine, _store);

    private byte[] Bytes => (byte[])((RegistryValue)_machine.Read(Mask)!).Data;

    [Fact]
    public void Live_state_reads_just_the_bit()
    {
        Assert.Equal("on", Assert.IsType<LiveState.InOption>(Engine().ReadLiveState(Tweak)).Option.Id);
    }

    [Fact]
    public void Apply_clears_only_its_bit_and_leaves_every_other_bit_alone()
    {
        Engine().Apply(Tweak, Tweak.Options[0]);

        Assert.Equal(new byte[] { 0x9C, 0x1E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00 }, Bytes);
    }

    [Fact]
    public void Undo_restores_only_its_bit_even_after_other_bits_changed()
    {
        var engine = Engine();
        engine.Apply(Tweak, Tweak.Options[0]);
        engine.Apply(ShadowTweak, ShadowTweak.Options[0]);

        engine.Undo(Tweak);

        // Menu animation is back on; the cursor shadow, turned off after, stays off.
        Assert.Equal(new byte[] { 0x9E, 0x1E & ~0x20, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00 }, Bytes);
        Assert.Null(_store.Get(Tweak.Id));
    }

    [Fact]
    public void Original_value_saved_is_the_bit_not_the_whole_mask()
    {
        Engine().Apply(Tweak, Tweak.Options[0]);

        var original = Assert.Single(_store.Get(Tweak.Id)!.OriginalValues);
        Assert.Equal(MenuAnimation, original.Location);
        Assert.Equal(BitValue.On, original.Value);
    }

    [Fact]
    public void Missing_mask_makes_the_tweak_unavailable()
    {
        var engine = new TweakEngine(new InMemoryMachine(), _store);

        Assert.IsType<LiveState.Unavailable>(engine.ReadLiveState(Tweak));
    }

    [Fact]
    public void A_mask_stored_as_text_is_unavailable_and_says_why()
    {
        // Akari-OS's reg.reg writes UserPreferencesMask as hex(2), an expandable string.
        var machine = new InMemoryMachine().With(Mask, new RegistryValue(RegistryValueKind.ExpandString, "ነ考"));

        var unavailable = Assert.IsType<LiveState.Unavailable>(new TweakEngine(machine, _store).ReadLiveState(Tweak));

        Assert.Contains("instead of binary", unavailable.Reason);
    }

    [Fact]
    public void Preview_lists_the_bit_change()
    {
        var change = Assert.Single(Engine().Preview(Tweak, Tweak.Options[0]));

        Assert.Equal(MenuAnimation, change.Location);
        Assert.Equal(BitValue.On, change.Before);
        Assert.Equal(BitValue.Off, change.After);
    }

    [Fact]
    public void Two_bits_in_one_tweak_are_written_together_and_rolled_back_together()
    {
        TweakTarget[] targets = [new(MenuAnimation), new(CursorShadow)];
        var both = new DeclaredTweak("both", "Both", "Two bits.", Category.Gaming, "Visual effects", targets,
        [
            new TweakOption("off", "Off", targets.ToDictionary(target => target, _ => (MachineValue?)BitValue.Off)),
            new TweakOption("on", "On", targets.ToDictionary(target => target, _ => (MachineValue?)BitValue.On)),
        ]);

        _machine.FailingWritesTo(CursorShadow);

        Assert.Throws<TweakApplyException>(() => Engine().Apply(both, both.Options[0]));
        Assert.Equal(new byte[] { 0x9E, 0x1E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00 }, Bytes);
    }
}
