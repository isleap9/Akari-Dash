using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class TweakEngineActivationTests
{
    private static readonly DeclaredTweak Immediate = Tweak("immediate", Activation.Immediately);
    private static readonly DeclaredTweak SignOut = Tweak("sign-out", Activation.AfterSignOut);
    private static readonly DeclaredTweak Restart = Tweak("restart", Activation.AfterRestart);
    private static readonly DeclaredTweak OtherRestart = Tweak("other-restart", Activation.AfterRestart);

    private readonly InMemoryMachine _machine = new();

    private static DeclaredTweak Tweak(string id, Activation activation)
    {
        var target = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Akari\Test", id));
        return new DeclaredTweak(
            id, id, "A test Tweak.", Category.Gaming, "Test Group", [target],
            [Option(target, "off", 0), Option(target, "on", 1)],
            Activation: activation);
    }

    private static TweakOption Option(TweakTarget target, string id, int value) =>
        new(id, id, new Dictionary<TweakTarget, MachineValue?> { [target] = RegistryValue.DWord(value) });

    private TweakEngine Engine() => new(_machine);

    [Fact]
    public void A_tweak_takes_effect_immediately_unless_declared_otherwise()
    {
        var tweak = new DeclaredTweak("test", "Test", "A test Tweak.", Category.Gaming, "Test Group", [], []);

        Assert.Equal(Activation.Immediately, tweak.Activation);
    }

    // One Option may take longer than the rest: UAC's levels are immediate, but the Option that
    // switches UAC itself off needs a restart. The Tweak stays Immediately and the Option says so.
    [Fact]
    public void An_option_that_waits_on_a_restart_makes_the_tweak_pending_a_restart()
    {
        var target = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Akari\Test", "level"));
        var now = Option(target, "now", 1);
        var later = new TweakOption(
            "later", "later", new Dictionary<TweakTarget, MachineValue?> { [target] = RegistryValue.DWord(0) },
            Activation.AfterRestart);
        var tweak = new DeclaredTweak(
            "levels", "Levels", "A test Tweak.", Category.Gaming, "Test Group", [target], [now, later],
            Activation: Activation.Immediately);
        var engine = Engine();

        Assert.Equal(Activation.Immediately, tweak.ActivationOf(now));
        Assert.Equal(Activation.AfterRestart, tweak.ActivationOf(later));

        engine.Apply(tweak, now);
        Assert.True(engine.Pending.IsEmpty);

        engine.Apply(tweak, later);
        Assert.Equal([tweak], engine.Pending.AfterRestart);
        Assert.True(engine.Pending.NeedsRestart);

        // Back to an immediate Option: the Tweak is no longer waiting on anything.
        engine.Apply(tweak, now);
        Assert.True(engine.Pending.IsEmpty);
    }

    // Putting back what the waiting Option wrote waits just as long.
    [Fact]
    public void Undoing_an_option_that_waited_on_a_restart_waits_on_one_too()
    {
        var target = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Akari\Test", "level"));
        var later = new TweakOption(
            "later", "later", new Dictionary<TweakTarget, MachineValue?> { [target] = RegistryValue.DWord(0) },
            Activation.AfterRestart);
        var tweak = new DeclaredTweak(
            "levels", "Levels", "A test Tweak.", Category.Gaming, "Test Group", [target],
            [Option(target, "now", 1), later]);
        var engine = Engine();

        engine.Apply(tweak, later);
        engine.Undo(tweak);

        Assert.Equal([tweak], engine.Pending.AfterRestart);
    }

    [Fact]
    public void Nothing_is_pending_before_any_tweak_is_changed()
    {
        Assert.True(Engine().Pending.IsEmpty);
    }

    [Fact]
    public void Applying_an_immediate_tweak_leaves_nothing_pending()
    {
        var engine = Engine();

        engine.Apply(Immediate, Immediate.Options[1]);

        Assert.True(engine.Pending.IsEmpty);
    }

    [Fact]
    public void Applying_sign_out_and_restart_tweaks_lists_each_under_its_activation()
    {
        var engine = Engine();

        engine.Apply(SignOut, SignOut.Options[1]);
        engine.Apply(Restart, Restart.Options[1]);
        engine.Apply(Immediate, Immediate.Options[1]);

        Assert.Equal([SignOut], engine.Pending.AfterSignOut);
        Assert.Equal([Restart], engine.Pending.AfterRestart);
        Assert.False(engine.Pending.IsEmpty);
        Assert.True(engine.Pending.NeedsRestart);
    }

    [Fact]
    public void Only_sign_out_tweaks_pending_does_not_need_a_restart()
    {
        var engine = Engine();
        engine.Apply(SignOut, SignOut.Options[1]);

        engine.Undo(SignOut);

        Assert.Equal([SignOut], engine.Pending.AfterSignOut);
        Assert.False(engine.Pending.NeedsRestart);
    }

    [Fact]
    public void A_tweak_changed_more_than_once_is_listed_once_in_the_order_first_changed()
    {
        var engine = Engine();

        engine.Apply(Restart, Restart.Options[1]);
        engine.Apply(OtherRestart, OtherRestart.Options[1]);
        engine.Apply(Restart, Restart.Options[0]);

        Assert.Equal([Restart, OtherRestart], engine.Pending.AfterRestart);
    }

    [Fact]
    public void Undoing_a_restart_tweak_leaves_it_pending()
    {
        var engine = Engine();
        engine.Apply(Restart, Restart.Options[1]);

        engine.Undo(Restart);

        Assert.Equal([Restart], engine.Pending.AfterRestart);
    }

    [Fact]
    public void Undoing_a_tweak_that_was_never_applied_leaves_nothing_pending()
    {
        var engine = Engine();

        engine.Undo(Restart);

        Assert.True(engine.Pending.IsEmpty);
    }

    [Fact]
    public void A_failed_apply_leaves_nothing_pending()
    {
        var target = Restart.Targets[0];
        var machine = new InMemoryMachine().FailingWritesTo(target.Location);
        var engine = new TweakEngine(machine);

        Assert.Throws<TweakApplyException>(() => engine.Apply(Restart, Restart.Options[1]));

        Assert.True(engine.Pending.IsEmpty);
    }
}
