using System.Diagnostics;

namespace AkariDash.App.Services;

/// <summary>What a finished process left behind.</summary>
internal sealed record ProcessRun(int ExitCode, string Output, string Error)
{
    public string? LastLine => LastLineOf(Output) ?? LastLineOf(Error);

    private static string? LastLineOf(string? text) =>
        text?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
}

/// <summary>Runs one process with no window and reads what it says, for the Action services.</summary>
internal static class ProcessRunner
{
    /// <summary>
    /// Runs <paramref name="fileName"/> to the end, or returns <see langword="null"/> when it could not
    /// be started at all (not on this PC, or refused).
    /// </summary>
    public static async Task<ProcessRun?> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        string? workingDirectory = null)
    {
        var start = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        if (workingDirectory is not null)
        {
            start.WorkingDirectory = workingDirectory;
        }

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Exception)
        {
            return null;
        }

        if (process is null)
        {
            return null;
        }

        using (process)
        {
            // Both pipes are drained at once: an installer that fills stderr while we are still
            // waiting on stdout would otherwise deadlock the two of us against each other.
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await Task.WhenAll(stdout, stderr);
            await process.WaitForExitAsync(cancellationToken);

            return new ProcessRun(process.ExitCode, await stdout, await stderr);
        }
    }
}
