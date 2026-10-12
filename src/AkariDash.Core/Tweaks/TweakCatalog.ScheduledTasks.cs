using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

// Scheduled tasks, one "Scheduled tasks" section on the Gaming page as in Winhance (MIT, 4dc441a0),
// which also lists PcaPatchDbTask. Task paths are confirmed on Windows itself in the VM. Most ids
// still start with "services." because the tasks used to be on the Services page and the ids are
// the keys of users' saved Original Values. On an SSD the defrag task is what sends TRIM, so it
// has no Recommended Option; only background telemetry with nothing visible behind it does.
public static partial class TweakCatalog
{
    private static IEnumerable<DeclaredTweak> ScheduledTaskTweaks() =>
    [
        TaskTweak("services.scheduled-drive-optimization", "Scheduled drive optimization",
            "Windows defragments hard drives and trims SSDs on a weekly schedule in the background. Turning it off stops it starting mid-game; you can still optimize drives by hand.",
            "Scheduled tasks", [@"\Microsoft\Windows\Defrag\ScheduledDefrag"]),

        TaskTweak("services.compatibility-appraiser", "Compatibility Appraiser",
            "Scans installed apps for upgrade compatibility and sends the results to Microsoft, which can use a lot of CPU and disk. Turned off, nothing visible changes.",
            "Scheduled tasks", [@"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser"], recommendOff: true),
        TaskTweak("services.program-data-updater", "Program data updater",
            "Collects data about installed programs for Microsoft. Turned off, nothing visible changes.",
            "Scheduled tasks", [@"\Microsoft\Windows\Application Experience\ProgramDataUpdater"], recommendOff: true),
        TaskTweak("services.compatibility-backup", "Compatibility data backup",
            "Backs up app compatibility data for Microsoft. Turned off, nothing visible changes.",
            "Scheduled tasks", [@"\Microsoft\Windows\Application Experience\MareBackup"], recommendOff: true),
        TaskTweak("services.ceip-consolidator", "Customer Experience data",
            "Gathers and sends usage data for the Customer Experience Improvement Program. Turned off, nothing visible changes.",
            "Scheduled tasks", [@"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator"], recommendOff: true),
        TaskTweak("services.usb-ceip", "USB usage data",
            "Collects data about your USB devices for Microsoft. Turned off, nothing visible changes.",
            "Scheduled tasks", [@"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip"], recommendOff: true),
        TaskTweak("services.sqm-tasks", "Software quality data",
            "Collects software quality data for Microsoft. Turned off, nothing visible changes.",
            "Scheduled tasks", [@"\Microsoft\Windows\PI\Sqm-Tasks"], recommendOff: true),
        TaskTweak("services.disk-diagnostic", "Disk diagnostic data",
            "Collects disk health data to send to Microsoft. Turned off, Windows no longer sends it; SMART warnings from your drive still show.",
            "Scheduled tasks", [@"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector"], recommendOff: true),
        TaskTweak("services.autochk-proxy", "Disk check data",
            "Sends data from startup disk checks to Microsoft. Turned off, nothing visible changes.",
            "Scheduled tasks", [@"\Microsoft\Windows\Autochk\Proxy"]),
        TaskTweak("services.feedback", "Feedback data",
            "Downloads feedback surveys and scenarios for Feedback Hub. Turned off, Windows stops asking for feedback on its own; Feedback Hub still opens.",
            "Scheduled tasks", [@"\Microsoft\Windows\Feedback\Siuf\DmClient", @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload"]),
        TaskTweak("services.error-report-queue", "Queued error reports",
            "Sends error reports that were saved for later. Turned off, they stay on this PC.",
            "Scheduled tasks", [@"\Microsoft\Windows\Windows Error Reporting\QueueReporting"]),
        TaskTweak("services.power-diagnostics", "Power efficiency analysis",
            "Analyzes this PC's power use on a schedule. Turned off, the analysis stops; you can still run it by hand.",
            "Scheduled tasks", [@"\Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem"]),

        TaskTweak("services.startup-app-task", "Startup app check",
            "Checks which apps start with Windows and how much they slow it down. Turned off, Windows stops telling you about new startup apps, and Task Manager's startup impact may stay empty.",
            "Scheduled tasks", [@"\Microsoft\Windows\Application Experience\StartupAppTask"]),
        TaskTweak("gaming.pca-patch-db", "Compatibility fixes update",
            "Updates the Program Compatibility Assistant's list of fixes for older apps. Turned off, the list stops updating; apps already fixed keep working.",
            "Scheduled tasks", [@"\Microsoft\Windows\Application Experience\PcaPatchDbTask"]),
        TaskTweak("services.maps-update", "Offline map updates",
            "Updates downloaded maps on a schedule. Turned off, they only update when you ask.",
            "Scheduled tasks", [@"\Microsoft\Windows\Maps\MapsUpdateTask"]),
        TaskTweak("services.family-safety", "Family Safety monitor",
            "Checks Microsoft Family settings for this PC. Turned off, Family limits may not apply.",
            "Scheduled tasks", [@"\Microsoft\Windows\Shell\FamilySafetyMonitor"]),
        TaskTweak("services.recall", "Recall",
            "Sets up and runs Recall, which saves snapshots of your screen on Copilot+ PCs. Turned off, Recall stops taking snapshots.",
            "Scheduled tasks", [@"\Microsoft\Windows\WindowsAI\RecallConfiguration", @"\Microsoft\Windows\WindowsAI\RecallPipeline"]),
        TaskTweak("services.office-actions", "Office Actions Server",
            "Runs background actions for Microsoft Office. Turned off, some Office features that work in the background may stop.",
            "Scheduled tasks", [@"\Microsoft\Office\Office Actions Server"]),
    ];

    private static DeclaredTweak TaskTweak(string id, string title, string description, string group, string[] paths, bool recommendOff = false)
    {
        var targets = paths.Select(path => new TweakTarget(new ScheduledTaskLocation(path))).ToList();
        var off = Option(targets, "off", "Off", TaskEnabledValue.Off);

        return new DeclaredTweak(id, title, description, Category.Gaming, group, targets, [off, Option(targets, "on", "On", TaskEnabledValue.On)],
            Recommended: recommendOff ? off : null);
    }
}
