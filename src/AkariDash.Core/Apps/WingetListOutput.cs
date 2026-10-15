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

        // winget truncates every column of a narrow table. A cut-off name, or the id of a package
        // we do not offer, cannot change our answer; a cut-off id of one of ours can, and that one
        // still means "we do not know".
        if (tokens.Any(token => token.EndsWith('…') && CouldBeOneOfOurs(token, apps)))
        {
            return null;
        }

        var listed = new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);
        return apps.Where(app => listed.Contains(app.WingetId))
            .Select(app => app.WingetId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Could this truncated token be the start of an id we offer?</summary>
    private static bool CouldBeOneOfOurs(string token, IReadOnlyList<InstallableApp> apps)
    {
        var prefix = token[..^1];
        return apps.Any(app => app.WingetId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
