namespace AkariDash.Core.Apps;

/// <summary>What kind of install an entry is: a Microsoft Store (Appx/MSIX) package or a classic Win32 uninstall entry.</summary>
public enum InstalledAppKind
{
    Appx,
    Win32,
}

/// <summary>
/// One uninstallable app: a Store package or a Win32 program from the Uninstall registry keys.
/// <paramref name="IconPath"/> is the file its icon comes from — a Store package's logo image, or the
/// program file a Win32 entry's DisplayIcon names — or <see langword="null"/> when it has none.
/// </summary>
public sealed record InstalledApp(
    string Id,
    string Name,
    string Publisher,
    string Version,
    InstalledAppKind Kind,
    string? PackageFullName = null,
    string? UninstallString = null,
    string? QuietUninstallString = null,
    string? IconPath = null);
