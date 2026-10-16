using System.Diagnostics;
using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;

// The safe-mode half of Defender optimize, the one change Windows lets be made only in safe boot as
// TrustedInstaller. Windows starts this once, from RunOnce, in safe mode.
//
// It runs in two modes. Run with --elevated it is the worker, started as TrustedInstaller by
// sc.exe, and all it does is write and record how that went. Run any other way it is the
// orchestrator: it takes the PC out of safe mode before anything else, so whatever happens next the
// following restart is a normal one, then runs the worker and restarts.

var log = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Akari-Dash", "logs", "defender-reset.log");

var passPath = SafeModePass.DefaultPath;
for (var i = 0; i + 1 < args.Length; i++)
{
    if (args[i] == "--pass")
    {
        passPath = args[i + 1];
    }
}

void Say(string text)
{
    Console.WriteLine(text);
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        File.AppendAllText(log, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {text}{Environment.NewLine}");
    }
    catch (IOException)
    {
        // The console still has it.
    }
}

// System32's own tool, never one found on PATH: what is on the PATH is whatever the machine says.
// The output is answered rather than printed, so the few answers that matter can be shown and the
// rest — sc.exe's complaints about a service that is not running, taskkill's about a process that
// never started — left out of a window the person is watching during a restart.
(int ExitCode, string Output) Run(string fileName, params string[] arguments)
{
    var inSystem32 = Path.Combine(Environment.SystemDirectory, fileName);
    var start = new ProcessStartInfo(File.Exists(inSystem32) ? inSystem32 : fileName)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
    };
    foreach (var argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }

    using var process = Process.Start(start) ?? throw new InvalidOperationException($"{fileName} could not be started.");
    var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
    process.WaitForExit();
    return (process.ExitCode, output);
}

SafeModePass? Read()
{
    try
    {
        return SafeModePass.Read(passPath);
    }
    catch (Exception ex)
    {
        Say($"Could not read {passPath}: {ex.Message}");
        return null;
    }
}

void Save(SafeModePass pass)
{
    try
    {
        pass.Write(passPath);
    }
    catch (Exception ex)
    {
        Say($"Could not write {passPath}: {ex.Message}");
    }
}

// Writes every value the pass calls for, and records the outcome in the pass file for the app to
// read. Each value is written on its own: one Windows refuses is reported and the rest still go on.
int Work()
{
    var pass = Read();
    if (pass is null)
    {
        Say($"There is no pass at {passPath}, so nothing was changed.");
        return 1;
    }

    Say($"{(pass.Undo ? "Putting Defender's settings back" : $"Applying {pass.OptionId}")} for {pass.TweakId}.");

    try
    {
        var machine = new WindowsMachine();
        var failures = new List<string>();
        var written = 0;

        foreach (var (location, value) in Values(pass).Concat(DefenderOptimize.Extra(optimize: !pass.Undo)))
        {
            try
            {
                if (location is not RegistryLocation && machine.Read(location) is null)
                {
                    // A missing boot setting is Windows' own, so silently nothing needs deleting.
                    if (location is BootSettingLocation)
                    {
                        continue;
                    }

                    Say($"Skipping {location}: it is not on this PC.");
                    continue;
                }

                if (value is null)
                {
                    machine.Delete(location);
                }
                else
                {
                    machine.Write(location, value);
                }

                written++;
            }
            catch (Exception ex)
            {
                failures.Add($"{location}: {ex.Message}");
            }
        }

        foreach (var failure in failures)
        {
            Say(failure);
        }

        Say(failures.Count == 0
            ? $"Wrote {written} value(s)."
            : $"Wrote {written} value(s); {failures.Count} could not be written.");

        if (pass.Undo && failures.Count == 0)
        {
            // Only now is it safe to forget the Original Values the Undo has just put back.
            new FileOriginalValuesStore(FileOriginalValuesStore.DefaultFolder).Clear(pass.TweakId);
        }

        Save(pass with
        {
            Stage = failures.Count == 0 ? SafeModePassStage.Applied : SafeModePassStage.Failed,
            Message = failures.Count == 0 ? null : string.Join("; ", failures),
        });

        return failures.Count == 0 ? 0 : 1;
    }
    catch (Exception ex)
    {
        Say($"The change did not run: {ex.Message}");
        Save(pass with { Stage = SafeModePassStage.Failed, Message = ex.Message });
        return 1;
    }
}

// What the pass calls for: the Tweak's own targets for an apply, and the Original Values Akari-Dash
// saved for an undo. AkariOS's Tweak puts its Optimize list back the same way, so both read the
// same catalog the app does. The targets Akari-Dash wrote in the session that asked are not written
// again: they are already on the machine, in the person's own profile.
IReadOnlyList<(MachineLocation Location, MachineValue? Value)> Values(SafeModePass pass)
{
    var tweak = TweakCatalog.All.SingleOrDefault(candidate => candidate.Id == pass.TweakId)
        ?? throw new InvalidOperationException($"Akari-Dash does not know the Tweak {pass.TweakId}.");
    var passTargets = DefenderOptimize.PassTargets(tweak).Select(target => target.Location).ToHashSet();

    if (pass.Undo)
    {
        var applied = new FileOriginalValuesStore(FileOriginalValuesStore.DefaultFolder).Get(pass.TweakId)
            ?? throw new InvalidOperationException(
                $"Akari-Dash has no record of {pass.TweakId}, so there is nothing to put back.");

        // What Akari-Dash wrote in the session that asked is put back there, not here, so the pass
        // puts back only its own, and safe mode is left whoever it signs in as.
        return [.. applied.OriginalValues.Where(original => passTargets.Contains(original.Location)).Select(original => (original.Location, original.Value))];
    }

    var option = tweak.Options.SingleOrDefault(candidate => candidate.Id == pass.OptionId)
        ?? throw new InvalidOperationException($"{pass.TweakId} has no Option {pass.OptionId}.");

    return [.. DefenderOptimize.PassTargets(tweak).Select(target => (target.Location, option.Values[target]))];
}

// The worker: Windows starts it inside TrustedInstaller's own process, which is the only account
// Defender's keys may be written from.
if (args.Contains("--elevated"))
{
    return Work();
}

Console.Title = "Akari Dash - Defender optimize";
Say("Akari Dash Defender optimize");

// First, before anything can fail: the next restart is a normal one.
var bcd = Run("bcdedit.exe", "/deletevalue", "{current}", "safeboot");
Say(bcd.ExitCode == 0
    ? "Safe mode is switched off for the next restart."
    : $"Safe mode could not be switched off: {bcd.Output.Trim()}");

var pass = Read();
if (pass is null)
{
    Say($"There is no pass at {passPath}, so nothing was changed.");
}
else
{
    Say(pass.Undo ? "Putting Defender's settings back." : "Applying Defender's settings.");
    RunTrusted();

    var result = Read();
    if (result is null || result.Stage == SafeModePassStage.Scheduled)
    {
        Say("The change could not be made as TrustedInstaller; nothing was changed.");
        if (result is not null)
        {
            Save(result with
            {
                Stage = SafeModePassStage.Failed,
                Message = "The change could not be made as TrustedInstaller; nothing was changed.",
            });
        }
    }
    else if (result.Stage == SafeModePassStage.Applied)
    {
        Say("Done.");
    }
    else
    {
        Say($"Not everything could be written: {result.Message}");
    }

    Say($"The details are in {log}.");
}

// A failed bcdedit would bring the PC straight back to safe mode, with nothing left to run. Say how
// to get out rather than restart into that silently.
if (bcd.ExitCode != 0)
{
    Say("The PC is still set to start in safe mode. Run this in an administrator Command Prompt, then restart:");
    Say("    bcdedit /deletevalue {current} safeboot");
    Say("Press Enter to close this window.");
    Console.ReadLine();
    return 1;
}

// Whatever happened, the PC has to restart: this session is safe mode, and the next one is normal
// Windows again because the safe-boot flag is already cleared.
Say("Restarting in 15 seconds.");
Run("shutdown.exe", "/r", "/t", "15");
Thread.Sleep(TimeSpan.FromSeconds(15));
return 0;

// Runs Work() as TrustedInstaller by pointing the TrustedInstaller service's own binary at this exe
// (AkariOS-Ultimate's Run-Trusted does the same, through cmd.exe and PowerShell). The service's
// command is put back whatever happens.
void RunTrusted()
{
    var entry = Environment.ProcessPath
        ?? throw new InvalidOperationException("This program's own path could not be read.");
    var command = $"\"{entry}\" --elevated --pass \"{passPath}\"";
    var defaultBinary = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "servicing", "TrustedInstaller.exe");

    StopTrustedInstaller();

    var configured = Run("sc.exe", "config", "TrustedInstaller", "binPath=", command);
    if (configured.ExitCode != 0)
    {
        Say($"TrustedInstaller's command could not be pointed at this program: {configured.Output.Trim()}");
        return;
    }

    try
    {
        var start = Run("sc.exe", "start", "TrustedInstaller");

        // This program does not speak the service control protocol, which is how it gets to run as
        // TrustedInstaller at all: the service starts it, it does the writing, and the Service
        // Control Manager gives up on hearing from it a few seconds later. So its answer is only
        // worth showing when nothing was recorded, and the pass file decides that, not sc.exe.
        for (var waited = 0; waited < 60 && Read() is { Stage: SafeModePassStage.Scheduled }; waited++)
        {
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }

        if (Read() is { Stage: SafeModePassStage.Scheduled })
        {
            Say($"TrustedInstaller was started but did not finish: {start.Output.Trim()}");
        }
    }
    finally
    {
        var restored = Run("sc.exe", "config", "TrustedInstaller", "binPath=", $"\"{defaultBinary}\"");
        if (restored.ExitCode != 0)
        {
            Say($"TrustedInstaller's own command could not be put back: {restored.Output.Trim()}");
            Say($"Put it back by hand: sc config TrustedInstaller binPath= \"{defaultBinary}\"");
        }

        StopTrustedInstaller();
    }
}

// Best effort, and never worth reporting: the service usually is not running to begin with, and its
// own answer — "the service is not started" — tells the person watching nothing they need.
void StopTrustedInstaller()
{
    try
    {
        Run("sc.exe", "stop", "TrustedInstaller");
        Run("taskkill.exe", "/im", "trustedinstaller.exe", "/f");
    }
    catch (Exception ex)
    {
        Say($"TrustedInstaller could not be stopped: {ex.Message}");
    }
}
