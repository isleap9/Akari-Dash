using AkariDash.App.Views;

namespace AkariDash.App;

/// <summary>Every page in the sidebar, in order. Home's quick links pick theirs from here too.</summary>
public static class ShellPages
{
    /// <summary>Navigation items for the shell NavigationView (top of the pane).</summary>
    public static IReadOnlyList<NavigationItem> Main { get; } =
    [
        new("Home", "\uE80F", typeof(HomePage), Subtitle: "Your gaming PC, tuned and reversible"),

        NavigationItem.Header("Performance"),
        new("Gaming", "\uE7FC", typeof(GamingPage), Subtitle: "Latency, GPU scheduling and the visual effects games pay for"),
        new("Drivers", "\uE7F8", typeof(DriversPage), Subtitle: "Clean the old graphics driver with DDU, then install and configure the new one"),
        new("Network", "\uE968", typeof(NetworkPage), Subtitle: "Work in progress"),
        new("Services", "\uE9F5", typeof(ServicesPage), Subtitle: "Background services and scheduled tasks"),

        NavigationItem.Header("Windows"),
        new("Privacy", "\uE72E", typeof(PrivacyPage), Subtitle: "Telemetry, tracking, suggestions and Windows AI"),
        new("Security", "\uEA18", typeof(SecurityPage), Subtitle: "Defender, virtualization-based security and mitigations"),
        new("Windows Update", "\uE777", typeof(UpdatePage), Subtitle: "When updates arrive, what comes with them and how loudly"),
        new("Notifications", "\uEA8F", typeof(NotificationsPage), Subtitle: "Toasts, tips and what Windows says while you play"),
        new("Windows", "\uE771", typeof(WindowsPage), Subtitle: "Theme, system behaviour and the rest of Windows"),
        new("Customize", "\uE790", typeof(CustomizePage), Subtitle: "Start menu, taskbar and File Explorer"),

        NavigationItem.Header("Apps"),
        new("UWP Apps", "\uE74D", typeof(UwpAppsPage), Subtitle: "Remove Windows apps for good, and bring them back"),
        new("Downloads", "\uE896", typeof(DownloadsPage), Subtitle: "Install the apps a gaming PC needs — launchers, runtimes and tools"),

        NavigationItem.Header("Advanced"),
        new("GPUs", "\uE950", typeof(GpusPage), Subtitle: "Work in progress"),
    ];

    /// <summary>Navigation items pinned to the bottom of the pane (footer).</summary>
    public static IReadOnlyList<NavigationItem> Footer { get; } =
    [
        new("Settings", "", typeof(SettingsPage), Subtitle: "Theme and about"),
    ];
}
