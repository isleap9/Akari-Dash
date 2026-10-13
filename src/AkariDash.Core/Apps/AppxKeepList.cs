namespace AkariDash.Core.Apps;

/// <summary>
/// The Store packages the Store apps tab never removes, and the ones Remove all bloatware leaves.
/// </summary>
public static class AppxKeepList
{
    // AkariOS-Ultimate's bloatware keep-list (MIT), with its reasons. Matched the way it matches them,
    // as a case-insensitive substring of the package name.
    private static readonly (string Pattern, string Reason)[] Protected =
    [
        ("CBS", "File Explorer breaks without it"),
        ("Microsoft.AV1VideoExtension", "a video codec"),
        ("Microsoft.AVCEncoderVideoExtension", "a video codec"),
        ("Microsoft.HEIFImageExtension", "an image codec"),
        ("Microsoft.HEVCVideoExtension", "a video codec"),
        ("Microsoft.MPEG2VideoExtension", "a video codec"),
        ("Microsoft.Paint", "kept by AkariOS-Ultimate"),
        ("Microsoft.RawImageExtension", "an image codec"),
        ("Microsoft.SecHealthUI", "Windows Security breaks without it"),
        ("Microsoft.VP9VideoExtensions", "a video codec"),
        ("Microsoft.WebMediaExtensions", "a media codec"),
        ("Microsoft.WebpImageExtension", "an image codec"),
        ("Microsoft.Windows.Photos", "kept by AkariOS-Ultimate"),
        ("Microsoft.Windows.ShellExperienceHost", "the taskbar breaks without it"),
        ("Microsoft.Windows.StartMenuExperienceHost", "the Start menu breaks without it"),
        ("Microsoft.WindowsNotepad", "kept by AkariOS-Ultimate"),
        ("NVIDIACorp.NVIDIAControlPanel", "NVIDIA's settings live in it"),
        ("windows.immersivecontrolpanel", "Settings breaks without it"),
    ];

    // Not in AkariOS-Ultimate's list, which removes the Store and reinstalls it on request. Here the
    // Restoration tab installs apps back through the Store and winget, so Remove all bloatware leaves
    // the two of them. Each can still be removed on its own.
    private static readonly string[] KeptForRestore =
    [
        "Microsoft.WindowsStore",
        "Microsoft.DesktopAppInstaller",
    ];

    /// <summary>Why the package must never be removed, or <see langword="null"/> when it may be.</summary>
    public static string? ProtectedBecause(string packageName) =>
        Protected.FirstOrDefault(entry => packageName.Contains(entry.Pattern, StringComparison.OrdinalIgnoreCase)).Reason;

    /// <summary>Whether Remove all bloatware removes the package.</summary>
    public static bool IsBloatware(StoreApp app) =>
        app.Kind == StoreAppKind.App &&
        ProtectedBecause(app.Name) is null &&
        !KeptForRestore.Contains(app.Name, StringComparer.OrdinalIgnoreCase);
}
