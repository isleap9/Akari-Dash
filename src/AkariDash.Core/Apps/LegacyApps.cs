namespace AkariDash.Core.Apps;

/// <summary>
/// A built-in desktop program the Uninstaller removes with AkariOS-Ultimate's script rather than its own
/// uninstaller, which refuses or does not exist, and Restoration reinstalls with AkariOS-Ultimate's
/// installer. <paramref name="UninstallNames"/> are the DisplayNames of its Uninstall registry entries,
/// exact or ending in <c>*</c> for a prefix, so the Uninstaller shows one row for it, not two.
/// <paramref name="InstallScript"/> is <see langword="null"/> for what Windows Update puts back itself
/// or nothing can, and <paramref name="Windows10Only"/> keeps a Windows 10 program's card off Windows 11.
/// </summary>
public sealed record LegacyApp(
    string Id,
    string Name,
    IReadOnlyList<string> UninstallNames,
    string Description,
    string Warning,
    string RemoveScript,
    string? InstallScript,
    bool Windows10Only = false)
{
    public bool Matches(string displayName) => UninstallNames.Any(name => name.EndsWith('*')
        ? displayName.StartsWith(name[..^1], StringComparison.OrdinalIgnoreCase)
        : string.Equals(displayName, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>The programs AkariOS-Ultimate (MIT) removes in its bloatware tweak and its Edge and WebView tweak.</summary>
public static class LegacyApps
{
    public static LegacyApp Edge { get; } = new(
        "edge",
        "Microsoft Edge",
        ["Microsoft Edge"],
        "Microsoft's browser. Its own uninstaller refuses outside the EU, so it is removed with AkariOS-Ultimate's script.",
        "Microsoft Edge is removed with AkariOS-Ultimate's script, which also removes Edge's updater.",
        "RemoveEdge.ps1",
        "InstallEdge.ps1");

    public static LegacyApp WebView { get; } = new(
        "webview",
        "Edge WebView2",
        ["Microsoft Edge WebView2 Runtime"],
        "Draws the windows of apps built on web pages. Widgets, the new Outlook and Teams need it.",
        "Removing WebView2 stops the apps that draw their window with it, Widgets, the new Outlook and Teams among them. Restoration can put it back.",
        "RemoveWebView.ps1",
        "InstallWebView.ps1");

    public static LegacyApp OneDrive { get; } = new(
        "onedrive",
        "Microsoft OneDrive",
        ["Microsoft OneDrive"],
        "Microsoft's cloud storage, syncing folders to a Microsoft account.",
        "OneDrive stops syncing. Files already on this PC stay; files only in the cloud stay in the cloud.",
        "RemoveOneDrive.ps1",
        "InstallOneDrive.ps1");

    public static LegacyApp RemoteDesktop { get; } = new(
        "remote-desktop",
        "Remote Desktop Connection",
        ["Remote Desktop Connection"],
        "Connects to another PC's desktop over the network (mstsc).",
        "Remote Desktop Connection is removed with its own /Uninstall switch.",
        "RemoveRemoteDesktop.ps1",
        "InstallRemoteDesktop.ps1");

    public static LegacyApp SnippingTool { get; } = new(
        "snipping-tool",
        "Snipping Tool (Windows 10)",
        [],
        "Windows 10's original screenshot tool.",
        "The Windows 10 Snipping Tool is removed with its own /Uninstall switch.",
        "RemoveSnippingTool.ps1",
        "InstallSnippingTool.ps1",
        Windows10Only: true);

    public static LegacyApp GameInput { get; } = new(
        "gameinput",
        "Microsoft GameInput",
        ["Microsoft GameInput*"],
        "The input service some newer games read controllers through.",
        "Games built on GameInput lose controller input until it is back. Restoration reinstalls it from winget.",
        "RemoveGameInput.ps1",
        "InstallGameInput.ps1");

    public static LegacyApp UpdateHealthTools { get; } = new(
        "update-health-tools",
        "Microsoft Update Health Tools",
        ["Microsoft Update Health Tools"],
        "Windows 10's helper that pushes feature updates. Windows Update reinstalls it when it wants it.",
        "Its service and its scheduled task go with it.",
        "RemoveUpdateHealthTools.ps1",
        null);

    public static LegacyApp UpdateForWindows { get; } = new(
        "update-for-windows",
        "Update for x64-based Windows Systems",
        ["Update for x64-based Windows Systems*"],
        "A Windows 10 update installer left behind as a program. Windows Update reinstalls it when it wants it.",
        "Windows Update may install it again.",
        "RemoveUpdateForWindows.ps1",
        null);

    public static LegacyApp Brlapi { get; } = new(
        "brlapi",
        "Braille (brlapi)",
        [],
        "The BRLTTY braille display service Windows ships, with its folder in Windows\\brltty.",
        "The brlapi service is deleted and its folder removed. Nothing reinstalls it.",
        "RemoveBrlapi.ps1",
        null);

    public static IReadOnlyList<LegacyApp> All { get; } =
        [Edge, WebView, OneDrive, RemoteDesktop, SnippingTool, GameInput, UpdateHealthTools, UpdateForWindows, Brlapi];

    /// <summary>The legacy app an Uninstall registry entry belongs to, or <see langword="null"/>.</summary>
    public static LegacyApp? ForUninstallName(string displayName) => All.FirstOrDefault(app => app.Matches(displayName));
}

/// <summary>Removes and reinstalls legacy apps. An Action backend (ADR-0004): each runs once.</summary>
public interface ILegacyAppService
{
    bool IsInstalled(LegacyApp app);

    Task RemoveAsync(LegacyApp app, CancellationToken cancellationToken = default);

    Task InstallAsync(LegacyApp app, CancellationToken cancellationToken = default);
}
