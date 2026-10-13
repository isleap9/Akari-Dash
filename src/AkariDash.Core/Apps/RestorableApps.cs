namespace AkariDash.Core.Apps;

/// <summary>
/// An app the Restoration tab can put back: its package name, what to call it, one line on what it
/// is, and its Microsoft Store product id (checked with <c>winget show --source msstore</c> on
/// 2026-10-13), or <see langword="null"/> when the Store no longer offers it.
/// </summary>
public sealed record RestorableApp(string PackageName, string Name, string Description, string? StoreId);

/// <summary>
/// The apps Windows 11 ships that people remove. Curated (ADR-0005): the question for each is whether
/// someone who removed it might want it back, not whether a package by that name exists.
/// </summary>
public static class RestorableApps
{
    public static IReadOnlyList<RestorableApp> All { get; } =
    [
        new("Microsoft.WindowsStore", "Microsoft Store", "Where Store apps come from, and how the other cards here reinstall.", "9WZDNCRFJBMP"),
        new("Microsoft.MicrosoftStickyNotes", "Sticky Notes", "Notes pinned to the desktop.", "9NBLGGH4QGHW"),
        new("Microsoft.WindowsAlarms", "Clock", "Alarms, timers, a stopwatch and focus sessions.", "9WZDNCRFJ3PR"),
        new("Microsoft.WindowsCalculator", "Calculator", "Windows' calculator.", "9WZDNCRFHVN5"),
        new("Microsoft.ScreenSketch", "Snipping Tool", "Screenshots and screen recordings, and what Win+Shift+S opens.", "9MZ95KL8MR0L"),
        new("Microsoft.WindowsTerminal", "Terminal", "Tabs for PowerShell and Command Prompt.", "9N0DX20HK701"),
        new("Microsoft.WindowsCamera", "Camera", "Photos and video from a webcam.", "9WZDNCRFJBBG"),
        new("Microsoft.WindowsSoundRecorder", "Sound Recorder", "Records audio from a microphone.", "9WZDNCRFHWKN"),
        new("Microsoft.ZuneMusic", "Media Player", "Plays music and video files.", "9WZDNCRFJ3PT"),
        new("Microsoft.GamingApp", "Xbox", "Game Pass and the Xbox PC store. PC Game Pass games install through it.", "9MV0B5HZVK9Z"),
        new("Microsoft.XboxGamingOverlay", "Game Bar", "The Win+G overlay: captures, performance widgets and Xbox chat.", "9NZKPSTSNW4P"),
        new("Microsoft.GamingServices", "Gaming Services", "Game Pass titles and many Store games will not start without it.", "9MWPM2CQNLHN"),
        new("Microsoft.XboxIdentityProvider", "Xbox Identity Provider", "Signs games in to Xbox; some PC games need it to start or to sync saves.", "9WZDNCRD1HKW"),
        new("Microsoft.YourPhone", "Phone Link", "A phone's texts, calls and photos on the PC.", "9NMPJ99VJBWV"),
        new("Microsoft.OutlookForWindows", "Outlook (new)", "Mail and calendar, in place of the old Mail and Calendar apps.", "9NRX63209R7B"),
        new("MSTeams", "Teams", "Microsoft's chat and video calls.", "XP8BT8DW290MPQ"),
        new("Microsoft.Todos", "To Do", "A task list synced to a Microsoft account.", "9NBLGGH5R558"),
        new("Microsoft.MicrosoftOfficeHub", "Microsoft 365 Copilot", "The Office launcher, mostly there to sell a Microsoft 365 subscription.", "9WZDNCRD29V9"),
        new("Microsoft.Copilot", "Copilot", "Microsoft's AI assistant as an app.", "9NHT9RB2F4HD"),
        new("Clipchamp.Clipchamp", "Clipchamp", "Microsoft's video editor.", "9P1J8S7CCWWT"),
        new("Microsoft.PowerAutomateDesktop", "Power Automate", "Records and replays desktop tasks.", "9NFTCH6J7FHV"),
        new("MicrosoftCorporationII.QuickAssist", "Quick Assist", "Lets someone else see or control this PC to help.", "9P7BP5VNWKX5"),
        new("MicrosoftCorporationII.MicrosoftFamily", "Family Safety", "Screen time and content limits for child accounts.", "9PDJDJS743XF"),
        new("Microsoft.WindowsFeedbackHub", "Feedback Hub", "Sends feedback to Microsoft. Windows Insider builds need it.", "9NBLGGH4R32N"),
        new("Microsoft.GetHelp", "Get Help", "Microsoft's support chat.", "9PKDZBMV1H3T"),
        new("Microsoft.BingNews", "News", "Headlines from MSN.", "9WZDNCRFHVFW"),
        new("Microsoft.BingWeather", "Weather", "Forecasts from MSN.", "9WZDNCRFJ3Q2"),
        new("Microsoft.BingSearch", "Bing", "Bing web results inside Windows Search.", "9NZBF4GT040C"),
        new("MicrosoftWindows.Client.WebExperience", "Widgets", "The Widgets board and its news feed.", "9MSSGKG348SP"),
        new("Microsoft.MicrosoftSolitaireCollection", "Solitaire Collection", "Solitaire and other card games, with ads.", null),
    ];
}
