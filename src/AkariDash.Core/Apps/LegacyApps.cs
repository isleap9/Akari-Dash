namespace AkariDash.Core.Apps;

/// <summary>
/// A built-in desktop program whose own uninstaller refuses, so the Uninstaller removes it with
/// AkariOS-Ultimate's script instead, and Restoration puts it back with AkariOS-Ultimate's installer.
/// <paramref name="UninstallName"/> is the DisplayName of its Uninstall registry entry, which is how the
/// Uninstaller recognises the row; <paramref name="Warning"/> is added to the uninstall confirmation.
/// </summary>
public sealed record LegacyApp(string Id, string Name, string UninstallName, string Description, string Warning, Uri Installer);

/// <summary>The programs removed by AkariOS-Ultimate's scripts (MIT). More arrive one at a time.</summary>
public static class LegacyApps
{
    public static LegacyApp Edge { get; } = new(
        "edge",
        "Microsoft Edge",
        "Microsoft Edge",
        "Microsoft's browser. Its own uninstaller refuses outside the EU, so it is removed with AkariOS-Ultimate's script.",
        "Microsoft Edge is removed with AkariOS-Ultimate's script, which also removes Edge's updater.",
        new Uri("https://github.com/isleap9/AkariOS-Files/releases/download/Files/edge.exe"));

    public static LegacyApp WebView { get; } = new(
        "webview",
        "Edge WebView2",
        "Microsoft Edge WebView2 Runtime",
        "Draws the windows of apps built on web pages. Widgets, the new Outlook and Teams need it.",
        "Removing WebView2 stops the apps that draw their window with it, Widgets, the new Outlook and Teams among them. Restoration can put it back.",
        new Uri("https://github.com/isleap9/AkariOS-Files/releases/download/Files/edgewebview.exe"));

    public static IReadOnlyList<LegacyApp> All { get; } = [Edge, WebView];

    /// <summary>The legacy app an Uninstall registry entry belongs to, or <see langword="null"/>.</summary>
    public static LegacyApp? ForUninstallName(string displayName) =>
        All.FirstOrDefault(app => string.Equals(app.UninstallName, displayName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Removes and reinstalls legacy apps. An Action backend (ADR-0004): each runs once.</summary>
public interface ILegacyAppService
{
    bool IsInstalled(LegacyApp app);

    Task RemoveAsync(LegacyApp app, CancellationToken cancellationToken = default);

    Task InstallAsync(LegacyApp app, CancellationToken cancellationToken = default);
}
