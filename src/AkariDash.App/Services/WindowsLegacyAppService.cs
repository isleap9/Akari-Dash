using System.Diagnostics;
using System.Reflection;
using AkariDash.Core.Apps;

namespace AkariDash.App.Services;

/// <summary>
/// Runs AkariOS-Ultimate's own removal and install scripts for each legacy app. They ship inside the
/// app (Scripts\*.ps1, embedded) and run in Windows PowerShell, elevated as Akari-Dash already is.
/// </summary>
public sealed class WindowsLegacyAppService : ILegacyAppService
{
    public bool IsInstalled(LegacyApp app) => app.Id switch
    {
        "edge" => File.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe")),
        _ => throw new ArgumentOutOfRangeException(nameof(app), app.Id, "Not a legacy app."),
    };

    public Task RemoveAsync(LegacyApp app, CancellationToken cancellationToken = default) =>
        RunScriptAsync(app, app.Id switch { "edge" => "RemoveEdge.ps1", _ => throw Unknown(app) }, "remove", cancellationToken);

    public Task InstallAsync(LegacyApp app, CancellationToken cancellationToken = default) =>
        RunScriptAsync(app, app.Id switch { "edge" => "InstallEdge.ps1", _ => throw Unknown(app) }, "install", cancellationToken);

    private static ArgumentOutOfRangeException Unknown(LegacyApp app) => new(nameof(app), app.Id, "Not a legacy app.");

    private static async Task RunScriptAsync(LegacyApp app, string script, string verb, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"AkariDash-{Guid.NewGuid():N}-{script}");
        await using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream($"AkariDash.App.Scripts.{script}")
            ?? throw new InvalidOperationException($"{script} is missing from this build."))
        await using (var target = File.Create(path))
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo("powershell.exe")
            {
                ArgumentList = { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", path },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }) ?? throw new InvalidOperationException("Windows PowerShell did not start.");

            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            // The scripts carry on past what fails, as AkariOS-Ultimate's do, so the result is read off
            // the machine rather than the exit code.
            var installed = new WindowsLegacyAppService().IsInstalled(app);
            if (verb == "remove" ? installed : !installed)
            {
                var detail = (await errors).Trim();
                throw new InvalidOperationException(
                    $"Could not {verb} {app.Name}.{(detail.Length == 0 ? string.Empty : $" {detail.Split('\n')[0].Trim()}")}");
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
