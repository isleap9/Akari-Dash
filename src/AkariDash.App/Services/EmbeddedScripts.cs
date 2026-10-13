using System.Diagnostics;
using System.Reflection;

namespace AkariDash.App.Services;

/// <summary>
/// AkariOS-Ultimate's scripts that ship inside the app (Scripts\*.ps1, embedded), run in Windows
/// PowerShell, elevated as Akari-Dash already is.
/// </summary>
internal static class EmbeddedScripts
{
    /// <summary>Runs a script and returns what it wrote to standard error.</summary>
    public static async Task<string> RunAsync(string script, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(Path.GetTempPath(), $"AkariDash-{Guid.NewGuid():N}-{script}");
        await using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream($"AkariDash.App.Scripts.{script}")
            ?? throw new InvalidOperationException($"{script} is missing from this build."))
        await using (var target = File.Create(path))
        {
            // Windows PowerShell's progress bar slows IWR downloads to a crawl, and nobody sees it here.
            await target.WriteAsync("$ProgressPreference = 'SilentlyContinue'\r\n"u8.ToArray(), cancellationToken);
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
            return await errors;
        }
        finally
        {
            File.Delete(path);
        }
    }
}
