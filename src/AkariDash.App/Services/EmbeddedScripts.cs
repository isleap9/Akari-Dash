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

    /// <summary>
    /// Runs one of AkariOS-Ultimate's Graphics scripts (Scripts\Graphics\*.ps1) the way Akari.ps1 runs
    /// it: after its helpers, in a single-threaded apartment so Get-FilePath can show a file dialog,
    /// with every line it writes handed to <paramref name="onLine"/>.
    /// </summary>
    public static async Task RunGraphicsAsync(string script, Action<string> onLine, CancellationToken cancellationToken = default)
    {
        var helpers = await ReadAsync($"Graphics.{AkariDash.Core.Drivers.GraphicsEntries.Helpers}", cancellationToken);
        var body = await ReadAsync($"Graphics.{script}", cancellationToken);
        var path = Path.Combine(Path.GetTempPath(), $"AkariDash-{Guid.NewGuid():N}-{script}");
        await File.WriteAllTextAsync(path, helpers + "\r\n" + body, System.Text.Encoding.UTF8, cancellationToken);

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("powershell.exe")
                {
                    ArgumentList = { "-NoProfile", "-Sta", "-ExecutionPolicy", "Bypass", "-File", path },
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };
            process.OutputDataReceived += (_, e) => Forward(e.Data);
            process.ErrorDataReceived += (_, e) => Forward(e.Data);
            if (!process.Start())
            {
                throw new InvalidOperationException("Windows PowerShell did not start.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            File.Delete(path);
        }

        void Forward(string? line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                onLine(line.TrimEnd());
            }
        }
    }

    private static async Task<string> ReadAsync(string name, CancellationToken cancellationToken)
    {
        await using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream($"AkariDash.App.Scripts.{name}")
            ?? throw new InvalidOperationException($"{name} is missing from this build.");
        using var reader = new StreamReader(source);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
