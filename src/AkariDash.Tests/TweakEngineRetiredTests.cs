using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

/// <summary>Tweaks Akari-Dash once applied that the catalog no longer has (issue #20).</summary>
public class TweakEngineRetiredTests
{
    private static readonly RegistryLocation Kept = new(RegistryHive.CurrentUser, @"Software\Akari\Test", "Kept");
    private static readonly RegistryLocation Existed = new(RegistryHive.CurrentUser, @"Software\Akari\Test", "Existed");
    private static readonly RegistryLocation Missing = new(RegistryHive.LocalMachine, @"Software\Akari\Test", "Missing");

    private static readonly TweakTarget KeptTarget = new(Kept);

    private static readonly DeclaredTweak StillInCatalog = new(
        "kept", "Kept", "A Tweak still in the catalog.", Category.Gaming, "Test Group", [KeptTarget],
        [
            new("zero", "Zero", new Dictionary<TweakTarget, MachineValue?> { [KeptTarget] = RegistryValue.DWord(0) }),
            new("one", "One", new Dictionary<TweakTarget, MachineValue?> { [KeptTarget] = RegistryValue.DWord(1) }),
        ]);

    // A dropped Tweak changed Existed from 5 to 0 and created Missing.
    private readonly InMemoryMachine _machine = new InMemoryMachine()
        .With(Existed, RegistryValue.DWord(0))
        .With(Missing, RegistryValue.DWord(1));

    private readonly InMemoryOriginalValuesStore _store = new();

    public TweakEngineRetiredTests()
    {
        _store.Save("dropped", new AppliedTweak("on", [new OriginalValue(Existed, RegistryValue.DWord(5)), new OriginalValue(Missing, null)]));
    }

    private TweakEngine Engine() => new(_machine, _store);

    [Fact]
    public void Lists_only_applied_tweaks_the_catalog_no_longer_has()
    {
        var engine = Engine();
        engine.Apply(StillInCatalog, StillInCatalog.Options[0]);

        var retired = Assert.Single(engine.RetiredTweaks([StillInCatalog]));

        Assert.Equal("dropped", retired.Id);
        Assert.Equal([Existed, Missing], retired.OriginalValues.Select(original => original.Location));
    }

    [Fact]
    public void Nothing_is_retired_when_every_applied_tweak_is_in_the_catalog()
    {
        _store.Clear("dropped");

        Assert.Empty(Engine().RetiredTweaks([StillInCatalog]));
    }

    [Fact]
    public void Undo_puts_back_the_saved_values_and_forgets_them()
    {
        var engine = Engine();

        engine.UndoRetired("dropped");

        Assert.Equal(RegistryValue.DWord(5), _machine.Read(Existed));
        Assert.Null(_machine.Read(Missing));
        Assert.Null(_store.Get("dropped"));
        Assert.Empty(engine.RetiredTweaks([StillInCatalog]));
    }

    [Fact]
    public void Undo_of_an_unknown_id_does_nothing()
    {
        Engine().UndoRetired("never-applied");

        Assert.Equal(RegistryValue.DWord(0), _machine.Read(Existed));
        Assert.NotNull(_store.Get("dropped"));
    }

    [Fact]
    public void A_failed_undo_keeps_the_saved_values_so_it_can_be_tried_again()
    {
        _machine.FailingWritesTo(Missing);

        Assert.Throws<TweakApplyException>(() => Engine().UndoRetired("dropped"));

        Assert.NotNull(_store.Get("dropped"));
        Assert.Equal(RegistryValue.DWord(0), _machine.Read(Existed));
    }
}
