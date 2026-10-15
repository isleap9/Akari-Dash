using AkariDash.Core.Apps;
using Microsoft.Extensions.Logging;

namespace AkariDash.App.Services;

/// <summary>
/// Installs the curated catalogue's apps with winget, and asks winget which of them are already
/// here. An Action backend (ADR-0004): an install runs once and cannot be undone. In the Phase 1
/// build nothing is installed at all - see <see cref="IsDryRun"/>.
/// </summary>
public sealed class WindowsWingetAppService(ILogger<WindowsWingetAppService> logger) : IAppInstallService
{
    public bool IsDryRun => BuildInfo.IsDryRunOnly;

    public async Task<IReadOnlySet<string>?> FindInstalledIdsAsync(
        IReadOnlyList<InstallableApp> apps,
        CancellationToken cancellationToken = default)
    {
        var run = await TryRunAsync(WingetCommand.List(), cancellationToken);
        if (run is null || run.ExitCode != 0)
        {
            // We could not get an answer, so we say nothing rather than "nothing is installed".
            logger.LogWarning("Could not ask winget what is installed; the Downloads page will not guess");
            return null;
        }

        return WingetListOutput.InstalledIds(run.Output, apps);
    }

    public async Task<InstallOutcome> InstallAsync(InstallableApp app, CancellationToken cancellationToken = default)
    {
        var arguments = WingetCommand.Install(app);

        if (IsDryRun)
        {
            // Phase 1 is safe to launch on any PC because it installs nothing. Say what a real
            // install would have run, then refuse rather than quietly claim a success that did
            // not happen.
            logger.LogInformation("Dry Run only: would have run {CommandLine}", WingetCommand.Describe(arguments));
            throw new InvalidOperationException(
                $"This is the Dry Run build, so \"{app.Name}\" was not installed.");
        }

        var run = await TryRunAsync(arguments, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Could not install \"{app.Name}\": winget is not available. Install App Installer from the Microsoft Store first.");

        var outcome = WingetInstallResult.Interpret(run.ExitCode);
        if (outcome is null)
        {
            var last = run.LastLine;
            throw new InvalidOperationException(
                $"Could not install \"{app.Name}\" (winget exit code 0x{run.ExitCode:X8}){(last is null ? "." : $": {last}")}");
        }

        return outcome.Value;
    }

    /// <summary>
    /// Runs one winget command, or <see langword="null"/> when winget is not on this PC at all.
    /// </summary>
    private async Task<ProcessRun?> TryRunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        logger.LogInformation("Running {CommandLine}", WingetCommand.Describe(arguments));

        var run = await ProcessRunner.RunAsync("winget", arguments, cancellationToken);
        if (run is null)
        {
            logger.LogWarning("winget could not be started");
        }

        return run;
    }
}
