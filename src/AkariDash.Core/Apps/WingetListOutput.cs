namespace AkariDash.Core.Apps;

/// <summary>Which of the catalogue's apps winget says are already on this machine.</summary>
public static class WingetListOutput
{
    /// <summary>
    /// The ids winget listed, or <see langword="null"/> when we cannot say - winget was not available,
    /// or its output was truncated so that "not listed" would not mean "not installed".
    /// </summary>
    public static IReadOnlySet<string>? InstalledIds(string? wingetListOutput, IReadOnlyList<InstallableApp> apps)
    {
        if (wingetListOutput is null)
        {
            return null;
        }

        var tokens = wingetListOutput
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        // A truncated id shares its prefix with another package, so a missing one proves nothing.
        if (tokens.Any(token => token.EndsWith('…')))
        {
            return null;
        }

        var listed = new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);
        return apps.Where(app => listed.Contains(app.WingetId))
            .Select(app => app.WingetId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
