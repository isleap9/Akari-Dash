using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

/// <summary>
/// The one Tweak Windows only lets be written in safe boot, so an apply or an Undo stages a pass for
/// the companion beside the app and restarts instead of writing.
/// </summary>
public class DefenderOptimizeTests
{
    private static DeclaredTweak Tweak => TweakCatalog.All.Single(tweak => tweak.Id == "security.defender-optimize");

    private static TweakOption Option(string id) => Tweak.Options.Single(option => option.Id == id);

    // The values are not written now: the PC has to be in safe mode, as TrustedInstaller, for Windows
    // to take them. The Original Values are saved first, so Undo has them whatever the pass does.
    [Fact]
    public void Applying_defender_optimize_stages_a_safe_mode_pass_and_writes_nothing()
    {
        var machine = DefenderMachine();
        var store = new InMemoryOriginalValuesStore();
        var engine = new TweakEngine(machine, store);

        engine.Apply(Tweak, Option("optimize"));

        var pass = Assert.Single(machine.SafeModePasses);
        Assert.Equal(new SafeModePass("security.defender-optimize", "optimize", Undo: false), pass);
        Assert.True(engine.IsApplied(Tweak));
        Assert.Equal("optimize", engine.AppliedOption(Tweak)!.Id);

        // Nothing was written: the machine does not hold the values the pass will write.
        Assert.Null(machine.Read(Tweak.Targets[0].Location));
    }

    // AkariOS writes its current-user values and its scheduled tasks in a normal boot, as admin, not in
    // safe boot: the current-user ones are in the person's own profile, and safe mode may sign in as
    // someone else, which would put them on the wrong one.
    [Fact]
    public void Applying_writes_the_values_that_do_not_need_safe_mode_now()
    {
        var machine = DefenderMachine();
        var engine = new TweakEngine(machine, new InMemoryOriginalValuesStore());

        engine.Apply(Tweak, Option("optimize"));

        // Edge's SmartScreen, the account protection reminders and the Defender tasks are in place now.
        var edge = new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenEnabled", "");
        Assert.Equal(RegistryValue.DWord(0), machine.Read(edge));
        Assert.Equal(
            RegistryValue.DWord(1),
            machine.Read(new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows Defender Security Center\Account protection", "DisableNotifications")));
        var scan = new ScheduledTaskLocation(@"\Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan");
        Assert.Equal(TaskEnabledValue.Off, machine.Read(scan));

        // What needs the keys only TrustedInstaller may write waits for the pass.
        Assert.Null(machine.Read(Tweak.Targets[0].Location));
        var pass = Assert.Single(machine.SafeModePasses);
        Assert.Equal(new SafeModePass("security.defender-optimize", "optimize", Undo: false), pass);
    }

    // The record has to survive the restart, because the companion reads the Original Values out of it
    // in safe mode; a pass that never ran therefore leaves Undo available, which is the honest reading.
    [Fact]
    public void Undoing_defender_optimize_stages_a_pass_and_keeps_the_original_values()
    {
        var machine = DefenderMachine();
        var store = new InMemoryOriginalValuesStore();
        var engine = new TweakEngine(machine, store);
        engine.Apply(Tweak, Option("optimize"));

        engine.Undo(Tweak);

        Assert.Equal(
            new SafeModePass("security.defender-optimize", "optimize", Undo: true),
            machine.SafeModePasses[1]);
        Assert.NotNull(store.Get("security.defender-optimize"));
        Assert.True(engine.IsApplied(Tweak));
    }

    // Undoing puts the session's own values back now, from the same record the pass reads in safe mode.
    [Fact]
    public void Undoing_puts_back_the_own_session_values_now_and_the_rest_in_safe_mode()
    {
        var machine = DefenderMachine();
        var store = new InMemoryOriginalValuesStore();
        var engine = new TweakEngine(machine, store);
        engine.Apply(Tweak, Option("optimize"));

        engine.Undo(Tweak);

        // The machine's own value for Edge's SmartScreen was that it did not exist, so Undo leaves it
        // absent again rather than writing a value Windows never had.
        var edge = new RegistryLocation(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Edge\SmartScreenEnabled", "");
        Assert.Null(machine.Read(edge));
        var scan = new ScheduledTaskLocation(@"\Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan");
        Assert.Equal(TaskEnabledValue.On, machine.Read(scan));

        // The rest is the pass's, and the record has to survive for it to read.
        Assert.Null(machine.Read(Tweak.Targets[0].Location));
        Assert.Equal(new SafeModePass("security.defender-optimize", "optimize", Undo: true), machine.SafeModePasses[1]);
        Assert.NotNull(store.Get("security.defender-optimize"));
    }

    // A pass target has to exist before the Tweak can be applied, so the fake machine holds Defender's
    // own tasks, as a PC would.
    private static InMemoryMachine DefenderMachine() =>
        new InMemoryMachine()
            .With(new ScheduledTaskLocation(@"\Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance"), TaskEnabledValue.On)
            .With(new ScheduledTaskLocation(@"\Microsoft\Windows\Windows Defender\Windows Defender Cleanup"), TaskEnabledValue.On)
            .With(new ScheduledTaskLocation(@"\Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan"), TaskEnabledValue.On)
            .With(new ScheduledTaskLocation(@"\Microsoft\Windows\Windows Defender\Windows Defender Verification"), TaskEnabledValue.On);

    // The companion is given the targets the pass writes and nothing else: what Akari-Dash wrote in the
    // session that asked is already on the machine, in the person's own profile.
    [Fact]
    public void The_companion_is_only_given_the_targets_the_safe_mode_pass_writes()
    {
        var passTargets = DefenderOptimize.PassTargets(Tweak).Select(target => target.Location).ToList();

        Assert.Empty(passTargets.OfType<ScheduledTaskLocation>());
        Assert.Empty(passTargets.OfType<RegistryLocation>().Where(location => location.Hive == RegistryHive.CurrentUser));
        Assert.True(passTargets.Contains(Tweak.Targets[0].Location));
        Assert.Equal(31, passTargets.Count);
    }

    // A Dry Run must show what would change without restarting anyone's PC.
    [Fact]
    public void A_dry_run_previews_the_change_without_staging_a_pass()
    {
        var machine = DefenderMachine();
        var dryRun = new DryRunMachine(machine);
        var engine = new TweakEngine(machine, new InMemoryOriginalValuesStore(), applyTo: dryRun);
        var optimize = Option("optimize");

        var preview = engine.Preview(Tweak, optimize);
        engine.Apply(Tweak, optimize);

        Assert.Empty(machine.SafeModePasses);

        // The ones the Optimize deletes are already absent on a PC nothing has been done to, so a Dry Run
        // shows nothing to change about them.
        var deletes = Tweak.Targets.Count(target => optimize.Values[target] is null);
        Assert.Equal(Tweak.Targets.Count - deletes, preview.Count);
    }

    // A safe-mode pass waits on a restart too, so the pages say so rather than "already applied".
    [Fact]
    public void A_staged_pass_is_pending_until_the_restart()
    {
        var engine = new TweakEngine(DefenderMachine(), new InMemoryOriginalValuesStore());

        engine.Apply(Tweak, Option("optimize"));

        var pending = engine.Pending;
        Assert.True(pending.NeedsRestart);
        Assert.Equal(Tweak, Assert.Single(pending.AfterRestart));
        Assert.Empty(pending.AfterSignOut);
    }

    // The key Windows will not let anyone but SYSTEM and TrustedInstaller read, and the Exploit Guard
    // task that needs no read-back, ride along in the pass instead of being targets.
    [Fact]
    public void Phishing_protection_and_the_exploit_guard_task_ride_along_in_the_pass()
    {
        var extras = DefenderOptimize.Extra(optimize: true);
        var phishing = extras.Select(extra => extra.Location).OfType<RegistryLocation>().ToList();
        var tasks = extras.Select(extra => extra.Location).OfType<ScheduledTaskLocation>().ToList();

        Assert.All(phishing, location => Assert.Equal(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components", location.Key));
        Assert.Equal(
            ["CaptureThreatWindow", "NotifyMalicious", "NotifyPasswordReuse", "NotifyUnsafeApp", "ServiceEnabled"],
            phishing.Select(location => location.Name));
        Assert.Equal(
            [@"\Microsoft\Windows\ExploitGuard\ExploitGuard MDM policy Refresh"],
            tasks.Select(task => task.Path));

        // Optimize writes 0 (off) and the Undo 1 (the Windows default).
        Assert.All(phishing, location => Assert.Equal(RegistryValue.DWord(0), extras.Single(extra => extra.Location == location).Value));
        Assert.All(
            DefenderOptimize.Extra(optimize: false).Where(extra => extra.Location is RegistryLocation),
            extra => Assert.Equal(RegistryValue.DWord(1), extra.Value));
    }

    // The task is the one extra that is not a registry value: Optimize stops it, the Undo starts it.
    [Fact]
    public void The_exploit_guard_task_is_stopped_by_optimize_and_started_by_the_undo()
    {
        static TaskEnabledValue? TaskValue(bool optimize) =>
            DefenderOptimize.Extra(optimize).Select(extra => extra.Value).OfType<TaskEnabledValue>().SingleOrDefault();

        Assert.Equal(TaskEnabledValue.Off, TaskValue(optimize: true));
        Assert.Equal(TaskEnabledValue.On, TaskValue(optimize: false));
    }
}
