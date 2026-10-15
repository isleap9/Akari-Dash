namespace AkariDash.Core.Drivers;

/// <summary>
/// The folder a person puts a downloaded driver installer in. There is no file picker: Akari-Dash
/// runs elevated, where the pickers do not open (see SystemCleanupViewModels' Drivers tab, which
/// works around the same thing with a fixed folder).
/// </summary>
public static class DriverInbox
{
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Akari-Dash", "Downloaded drivers");

    /// <summary>The installer files in the folder, newest first; folders and everything else are ignored.</summary>
    public static IReadOnlyList<string> Installers(IEnumerable<string> filePaths) =>
        filePaths
            .Where(path => string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
