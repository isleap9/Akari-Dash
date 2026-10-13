using System.Diagnostics;
using AkariDash.Core.Apps;

namespace AkariDash.App.Services;

/// <summary>
/// Removes legacy apps with AkariOS-Ultimate's own scripts (see <see cref="EmbeddedScripts"/>), and
/// reinstalls them with the installers AkariOS-Ultimate downloads from its AkariOS-Files release.
/// </summary>
public sealed class WindowsLegacyAppService : ILegacyAppService
{
    private static readonly HttpClient Http = new();

    private static string MicrosoftX86 => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft");

    public bool IsInstalled(LegacyApp app) => app.Id switch
    {
        "edge" => File.Exists(Path.Combine(MicrosoftX86, @"Edge\Application\msedge.exe")),
        "webview" => Directory.Exists(Path.Combine(MicrosoftX86, @"EdgeWebView\Application")),
        _ => throw Unknown(app),
    };

    public async Task RemoveAsync(LegacyApp app, CancellationToken cancellationToken = default)
    {
        var script = app.Id switch
        {
            "edge" => "RemoveEdge.ps1",
            "webview" => "RemoveWebView.ps1",
            _ => throw Unknown(app),
        };

        var errors = await EmbeddedScripts.RunAsync(script, cancellationToken);

        // The scripts carry on past what fails, as AkariOS-Ultimate's do, so the result is read off the
        // machine rather than the exit code.
        if (IsInstalled(app))
        {
            throw new InvalidOperationException($"Could not uninstall {app.Name}.{FirstLine(errors)}");
        }
    }

    public async Task InstallAsync(LegacyApp app, CancellationToken cancellationToken = default)
    {
        var installer = Path.Combine(Path.GetTempPath(), $"AkariDash-{Guid.NewGuid():N}-{Path.GetFileName(app.Installer.LocalPath)}");
        try
        {
            try
            {
                await using var download = await Http.GetStreamAsync(app.Installer, cancellationToken);
                await using var file = File.Create(installer);
                await download.CopyToAsync(file, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Could not download {app.Name}'s installer: {ex.Message}", ex);
            }

            using (var process = Process.Start(new ProcessStartInfo(installer) { UseShellExecute = false })
                ?? throw new InvalidOperationException($"{app.Name}'s installer did not start."))
            {
                await process.WaitForExitAsync(cancellationToken);
            }

            if (!IsInstalled(app))
            {
                throw new InvalidOperationException($"{app.Name}'s installer finished, but {app.Name} is not installed.");
            }
        }
        finally
        {
            File.Delete(installer);
        }
    }

    private static ArgumentOutOfRangeException Unknown(LegacyApp app) => new(nameof(app), app.Id, "Not a legacy app.");

    private static string FirstLine(string text) =>
        text.Trim() is { Length: > 0 } trimmed ? $" {trimmed.Split('\n')[0].Trim()}" : string.Empty;
}
