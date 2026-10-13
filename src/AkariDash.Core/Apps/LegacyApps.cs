namespace AkariDash.Core.Apps;

/// <summary>
/// A built-in Windows program the Store and Uninstaller tabs cannot remove, because its own uninstaller
/// refuses or it is not a package at all. Each one is AkariOS-Ultimate's removal and reinstall for it.
/// <paramref name="Warning"/> is what the confirmation says before removing it.
/// </summary>
public sealed record LegacyApp(string Id, string Name, string Description, string Warning);

/// <summary>The Legacy apps tab's programs, from AkariOS-Ultimate (MIT). More arrive one at a time.</summary>
public static class LegacyApps
{
    public static LegacyApp Edge { get; } = new(
        "edge",
        "Microsoft Edge and WebView2",
        "Edge, its updater and WebView2, which Edge's own uninstaller refuses to remove outside the EU.",
        "WebView2 goes with Edge, and apps that draw their window with it stop working: Widgets, the new Outlook and Teams among them. Install puts both back and needs an internet connection.");

    public static IReadOnlyList<LegacyApp> All { get; } = [Edge];
}

/// <summary>The Legacy apps tab's backend. An Action surface (ADR-0004): each removal and install runs once.</summary>
public interface ILegacyAppService
{
    bool IsInstalled(LegacyApp app);

    Task RemoveAsync(LegacyApp app, CancellationToken cancellationToken = default);

    Task InstallAsync(LegacyApp app, CancellationToken cancellationToken = default);
}
