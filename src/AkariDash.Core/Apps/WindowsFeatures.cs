namespace AkariDash.Core.Apps;

/// <summary>Which list a feature is on: Turn Windows features on or off, or Settings' Optional features.</summary>
public enum WindowsFeatureKind
{
    /// <summary>An optional feature (DISM's features; AkariOS-Ultimate's "Legacy Features").</summary>
    Feature,

    /// <summary>A capability (Settings > Optional features; AkariOS-Ultimate's "UWP Features").</summary>
    Capability,
}

/// <summary>One Windows feature or capability: its technical name, what to call it, and whether it is on.</summary>
public sealed record WindowsFeature(string Name, string DisplayName, WindowsFeatureKind Kind, bool IsOn);

/// <summary>
/// AkariOS-Ultimate's keep-lists for its bloatware tweak's "UWP Features" and "Legacy Features" removal
/// (MIT, Tweaks/Windows.ps1), with its reasons. Matched the way it matches them, as a case-insensitive
/// substring of the name. Remove all leaves these, and switching one off asks first.
/// </summary>
public static class WindowsFeatureKeepList
{
    private static readonly (string Pattern, string Reason)[] Capabilities =
    [
        ("Microsoft.Windows.Ethernet", "wired networking"),
        ("Microsoft.Windows.MSPaint", "Paint, on Windows 10"),
        ("Microsoft.Windows.Notepad", "Notepad"),
        ("Microsoft.Windows.Wifi", "Wi-Fi"),
        ("NetFX3", ".NET Framework 3.5, which older games need"),
        ("VBSCRIPT", "MSI installers break without it on Windows 11"),
        ("WMIC", "monitoring programs break without it"),
        ("Windows.Client.ShellComponents", "the Snipping Tool breaks without it on Windows 10"),
    ];

    private static readonly (string Pattern, string Reason)[] Features =
    [
        ("DirectPlay", "older games need it"),
        ("LegacyComponents", "older games need it"),
        ("NetFx3", ".NET Framework 3.5, which older games need"),
        ("NetFx4", ".NET Framework 4, which Turn Windows features on or off needs"),
        ("SearchEngine-Client-Package", "Windows Search breaks without it"),
        ("Server-Shell", "the Windows Server desktop breaks without it"),
        ("Windows-Defender", "Windows Defender breaks without it"),
        ("Server-Drivers-General", "networking on Windows Server breaks without it"),
        ("ServerCore-Drivers-General", "networking on Windows Server breaks without it"),
        ("Server-Gui-Mgmt", "Turn Windows features on or off breaks without it on Windows Server"),
        ("WirelessNetworking", "the NVIDIA app breaks without it on Windows Server"),
    ];

    /// <summary>Why the feature should stay on, or <see langword="null"/> when nothing depends on it.</summary>
    public static string? KeptBecause(WindowsFeature feature) =>
        (feature.Kind == WindowsFeatureKind.Capability ? Capabilities : Features)
            .FirstOrDefault(entry => feature.Name.Contains(entry.Pattern, StringComparison.OrdinalIgnoreCase)).Reason;

    /// <summary>Whether Remove all switches the feature off: it is on, and nothing on the keep-list.</summary>
    public static bool RemovedByRemoveAll(WindowsFeature feature) => feature.IsOn && KeptBecause(feature) is null;
}

/// <summary>
/// Lists Windows features and capabilities and switches them. An Action backend (ADR-0004): each change
/// runs once. Both switch methods return whether Windows needs a restart to finish.
/// </summary>
public interface IWindowsFeatureService
{
    Task<IReadOnlyList<WindowsFeature>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> TurnOnAsync(WindowsFeature feature, CancellationToken cancellationToken = default);

    Task<bool> TurnOffAsync(WindowsFeature feature, CancellationToken cancellationToken = default);
}
