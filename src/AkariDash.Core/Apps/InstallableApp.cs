namespace AkariDash.Core.Apps;

/// <summary>Which filter chip an installable app falls under.</summary>
public enum InstallableAppCategory
{
    Launchers,
    Communication,
    Tools,
    Monitoring,
    Runtimes,
}

/// <summary>
/// One app the Downloads page can install: its winget package id, what to call it, who publishes it,
/// one line on what it is, its filter chip, and the colour of its tile's avatar square.
/// </summary>
public sealed record InstallableApp(
    string WingetId,
    string Name,
    string Publisher,
    string Description,
    InstallableAppCategory Category,
    string AccentColor);
