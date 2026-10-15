namespace AkariDash.Core.Apps;

/// <summary>
/// The apps the Downloads page offers. Curated (ADR-0005): a list of what someone setting up a
/// gaming PC actually reaches for, not everything winget can install. Every id was checked with
/// <c>winget show --id &lt;id&gt; --exact --source winget</c> on 2026-10-15.
/// </summary>
public static class InstallableApps
{
    public static IReadOnlyList<InstallableApp> All { get; } =
    [
        new("Valve.Steam", "Steam", "Valve", "Play, install and update PC games.", InstallableAppCategory.Launchers, "#1B2838"),
        new("EpicGames.EpicGamesLauncher", "Epic Games Launcher", "Epic Games", "Epic's game store, and where Fortnite lives.", InstallableAppCategory.Launchers, "#2A2A2A"),
        new("Blizzard.BattleNet", "Battle.net", "Blizzard", "Blizzard's games, and the launcher that keeps them updated.", InstallableAppCategory.Launchers, "#00AEFF"),
        new("GOG.Galaxy", "GOG Galaxy", "GOG", "GOG's games, DRM-free and in one library.", InstallableAppCategory.Launchers, "#6C2BD9"),
        new("ElectronicArts.EADesktop", "EA App", "Electronic Arts", "EA's games, and the thing that replaced Origin.", InstallableAppCategory.Launchers, "#E4002B"),
        new("Ubisoft.Connect", "Ubisoft Connect", "Ubisoft", "Ubisoft's games, saves and rewards in one place.", InstallableAppCategory.Launchers, "#0070FF"),
        new("RockstarGames.Launcher", "Rockstar Games Launcher", "Rockstar Games", "Runs Grand Theft Auto and Red Dead on PC.", InstallableAppCategory.Launchers, "#FCAF17"),
        new("Discord.Discord", "Discord", "Discord Inc.", "Voice, video and text for playing with other people.", InstallableAppCategory.Communication, "#5865F2"),
        new("OBSProject.OBSStudio", "OBS Studio", "OBS Project", "Record the screen, or stream it.", InstallableAppCategory.Tools, "#302E31"),
        new("7zip.7zip", "7-Zip", "Igor Pavlov", "Opens almost any archive, and packs its own.", InstallableAppCategory.Tools, "#1F5FA9"),
        new("ShareX.ShareX", "ShareX", "ShareX Team", "Screenshots, screen recording and quick uploads.", InstallableAppCategory.Tools, "#2E8B57"),
        new("Notepad++.Notepad++", "Notepad++", "Notepad++ Team", "A text editor for code and configs.", InstallableAppCategory.Tools, "#90C31B"),
        new("Microsoft.PowerToys", "PowerToys", "Microsoft", "Microsoft's own set of small Windows utilities.", InstallableAppCategory.Tools, "#0078D4"),
        new("voidtools.Everything", "Everything", "voidtools", "Finds any file on the PC as you type.", InstallableAppCategory.Tools, "#E8762C"),
        new("VideoLAN.VLC", "VLC", "VideoLAN", "Plays nearly every video and audio file there is.", InstallableAppCategory.Tools, "#FF8800"),
        new("Guru3D.Afterburner", "MSI Afterburner", "MSI", "Overclocking, fan control and on-screen stats for the graphics card.", InstallableAppCategory.Monitoring, "#B01C1C"),
        new("REALiX.HWiNFO", "HWiNFO", "Martin Malík", "Reads every sensor in the machine, in detail.", InstallableAppCategory.Monitoring, "#16794A"),
        new("CPUID.CPU-Z", "CPU-Z", "CPUID", "Says exactly which processor, memory and board you have.", InstallableAppCategory.Monitoring, "#0F5FA6"),
        new("TechPowerUp.GPU-Z", "GPU-Z", "TechPowerUp", "Says exactly which graphics card you have.", InstallableAppCategory.Monitoring, "#2F6F9F"),
        new("Microsoft.VCRedist.2015+.x64", "Visual C++ 2015–2022 (x64)", "Microsoft", "The library most games and programs need before they will start.", InstallableAppCategory.Runtimes, "#0078D4"),
        new("Microsoft.DotNet.DesktopRuntime.8", ".NET Desktop Runtime 8", "Microsoft", "Runs desktop programs built with .NET.", InstallableAppCategory.Runtimes, "#512BD4"),
    ];
}
