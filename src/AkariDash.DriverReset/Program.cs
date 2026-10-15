using System.Diagnostics;
using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;

// The safe-mode half of a DDU clean. Windows starts this once, from RunOnce, in safe mode. It takes
// the PC out of safe mode before anything else, so whatever happens next the following restart is a
// normal one; then it runs DDU on the one card the person picked, writes down how that went for
// the app to read, and restarts.

var log = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Akari-Dash", "logs", "driver-reset.log");

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

int Run(string fileName, params string[] arguments)
{
    var start = new ProcessStartInfo(fileName) { UseShellExecute = false };
    foreach (var argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }

    using var process = Process.Start(start) ?? throw new InvalidOperationException($"{fileName} could not be started.");
    process.WaitForExit();
    return process.ExitCode;
}

Console.Title = "Akari Dash - driver clean";
Say("Akari Dash driver clean");

// First, before anything can fail: the next restart is a normal one.
var bcd = Run("bcdedit.exe", "/deletevalue", "{current}", "safeboot");
Say(bcd == 0 ? "Safe mode is switched off for the next restart." : $"bcdedit could not switch safe mode off (exit code {bcd}).");

var statePath = args.Length == 2 && args[0] == "--state" ? args[1] : DriverResetState.DefaultPath;
DriverResetState? state = null;
try
{
    state = DriverResetState.FromJson(File.Exists(statePath) ? File.ReadAllText(statePath) : null);
}
catch (IOException ex)
{
    Say($"Could not read {statePath}: {ex.Message}");
}

void Save(DriverResetState next)
{
    state = next;
    try
    {
        File.WriteAllText(statePath, next.ToJson());
    }
    catch (IOException ex)
    {
        Say($"Could not write {statePath}: {ex.Message}");
    }
}

if (state is null)
{
    Say("There is no driver clean to run, so nothing was removed.");
}
else
{
    var dduPath = Path.Combine(state.DduFolder, DriverClean.DduExecutable);
    try
    {
        if (!File.Exists(dduPath))
        {
            throw new FileNotFoundException($"DDU is not at {dduPath}.");
        }

        Save(state with { Stage = DriverResetStage.Cleaning });

        int exitCode;
        if (state.Manual)
        {
            Say($"Opening DDU. Clean the {state.Vendor.DisplayName()} driver, then close DDU: the PC restarts after it closes.");
            exitCode = Run(dduPath);
        }
        else
        {
            Say($"DDU is removing the {state.Vendor.DisplayName()} driver. This takes a minute or two; the screen may flicker.");
            exitCode = Run(dduPath, [.. DriverClean.Arguments(state.Vendor)]);
        }

        Say($"DDU finished (exit code {exitCode}).");
        Save(state with { Stage = DriverResetStage.Cleaned, ExitCode = exitCode });
    }
    catch (Exception ex)
    {
        Say($"The clean did not run: {ex.Message}");
        Save(state with { Stage = DriverResetStage.Failed, Message = ex.Message });
    }
}

// A failed bcdedit would bring the PC straight back to safe mode, with nothing left to run. Say how
// to get out rather than restart into that silently.
if (bcd != 0)
{
    Say("The PC is still set to start in safe mode. Run this in an administrator Command Prompt, then restart:");
    Say("    bcdedit /deletevalue {current} safeboot");
    Say("Press Enter to close this window.");
    Console.ReadLine();
    return 1;
}

Say("Restarting in 15 seconds.");
Run("shutdown.exe", "/r", "/t", "15");
Thread.Sleep(TimeSpan.FromSeconds(15));
return 0;
