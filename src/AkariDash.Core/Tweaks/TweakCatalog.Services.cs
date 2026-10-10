using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

// Services Tweaks: Windows services and scheduled tasks most gamers never use. Each service's
// "off" (Disabled) and Windows default start type are AkariOS-Ultimate's (MIT) services-off and
// services-default lists; which ones use delayed start, and every task path, are confirmed on
// Windows itself in the VM. Anything that breaks a feature people rely on has no Recommended
// Option; only background telemetry with nothing visible behind it recommends Off.
public static partial class TweakCatalog
{
    private const ServiceStartType Automatic = ServiceStartType.Automatic;
    private const ServiceStartType Delayed = ServiceStartType.AutomaticDelayed;
    private const ServiceStartType Manual = ServiceStartType.Manual;

    private static IEnumerable<DeclaredTweak> ServicesTweaks() =>
    [
        // Printing and fax
        ServiceTweak("services.print-spooler", "Print Spooler",
            "Sends documents to printers. Turned off, nothing can print, including Microsoft Print to PDF.",
            "Printing and fax", [("Spooler", Automatic)]),
        ServiceTweak("services.fax", "Fax",
            "Sends and receives faxes through a fax modem. Turned off, Windows Fax and Scan can no longer fax.",
            "Printing and fax", [("Fax", Manual)]),
        ServiceTweak("services.telephony", "Telephony",
            "Lets programs control modems and phone lines. Turned off, dial-up connections and older fax or phone software stop working.",
            "Printing and fax", [("TapiSrv", Manual)]),

        // Xbox
        ServiceTweak("services.xbox-sign-in", "Xbox Live sign-in",
            "Signs you in to Xbox services. Turned off, the Xbox app, Game Pass and Microsoft Store games that need an Xbox sign-in stop working.",
            "Xbox", [("XblAuthManager", Manual)]),
        ServiceTweak("services.xbox-game-saves", "Xbox cloud saves",
            "Syncs saved games to the cloud for games that use Xbox Live. Turned off, those saves stay on this PC only and can conflict later.",
            "Xbox", [("XblGameSave", Manual)]),
        ServiceTweak("services.xbox-networking", "Xbox networking",
            "Checks and repairs your connection for Xbox multiplayer and party chat. Turned off, the Xbox app can no longer test or fix your network.",
            "Xbox", [("XboxNetApiSvc", Manual)]),
        ServiceTweak("services.xbox-accessories", "Xbox accessories",
            "Configures Xbox controllers and accessories. Turned off, the Xbox Accessories app cannot remap buttons or update controller firmware; the controller itself still works.",
            "Xbox", [("XboxGipSvc", Manual)]),

        // Remote access
        ServiceTweak("services.remote-desktop", "Remote Desktop",
            "Lets other computers connect to this PC with Remote Desktop. Turned off, nobody can connect in; connecting from this PC to others still works.",
            "Remote access", [("TermService", Manual), ("SessionEnv", Manual), ("UmRdpService", Manual)]),
        ServiceTweak("services.vpn", "Built-in VPN and dial-up",
            "Connects VPNs and dial-up connections set up in Windows Settings. Turned off, those connections fail; VPN apps that bring their own driver are not affected.",
            "Remote access", [("RasMan", Manual), ("RasAuto", Manual)]),

        // Connected devices
        ServiceTweak("services.connected-devices", "Connected devices",
            "Links this PC with your phone and other devices. Turned off, Phone Link, Nearby sharing and syncing the clipboard across devices stop working.",
            "Connected devices", [("CDPSvc", Delayed), ("CDPUserSvc", Automatic)]),
        ServiceTweak("services.phone", "Phone calls",
            "Tracks the phone and call state for apps that make calls. Turned off, calling through Phone Link stops working.",
            "Connected devices", [("PhoneSvc", Manual)]),
        ServiceTweak("services.text-messages", "Text messages",
            "Routes text messages to apps that read or send them. Turned off, those apps can no longer send or receive texts.",
            "Connected devices", [("SmsRouter", Manual)]),
        ServiceTweak("services.mobile-hotspot", "Mobile hotspot",
            "Shares this PC's internet connection with other devices. Turned off, the mobile hotspot in Settings cannot be turned on.",
            "Connected devices", [("icssvc", Manual)]),

        // Hardware
        ServiceTweak("services.smart-cards", "Smart cards",
            "Reads smart cards and smart card readers, used for some work and government sign-ins. Turned off, smart cards stop working.",
            "Hardware", [("SCardSvr", Manual), ("ScDeviceEnum", Manual), ("SCPolicySvc", Manual)]),
        ServiceTweak("services.biometrics", "Fingerprint and face sign-in",
            "Runs fingerprint readers and face cameras. Turned off, Windows Hello fingerprint and face sign-in stop working; PIN and password still do.",
            "Hardware", [("WbioSrvc", Manual)]),
        ServiceTweak("services.sensors", "Sensors",
            "Reads light, motion and orientation sensors, mostly found in laptops and tablets. Turned off, automatic brightness and screen rotation stop working.",
            "Hardware", [("SensorService", Manual), ("SensrSvc", Manual), ("SensorDataService", Manual)]),
        ServiceTweak("services.touch-keyboard", "Touch keyboard and handwriting",
            "Runs the touch keyboard and pen handwriting panel. Turned off, these stop working, and so can the emoji panel and input methods for some languages.",
            "Hardware", [("TabletInputService", Manual)]),
        ServiceTweak("services.mixed-reality", "Mixed reality",
            "Runs Windows Mixed Reality headsets. Turned off, those headsets stop working; other VR headsets with their own software are not affected.",
            "Hardware", [("MixedRealityOpenXRSvc", Manual)]),

        // Payments
        ServiceTweak("services.wallet", "Wallet",
            "Stores payment details for apps that use the Windows wallet. Turned off, those apps cannot use it.",
            "Payments", [("WalletService", Manual)]),
        ServiceTweak("services.payments-nfc", "Payments and NFC",
            "Handles NFC and tap-to-pay hardware. Turned off, NFC payments from this PC stop working.",
            "Payments", [("SEMgrSvc", Manual)]),

        // Location and maps
        ServiceTweak("services.geolocation", "Location",
            "Works out where this PC is. Turned off, apps and Windows cannot use your location, for example for weather, Find my device or setting the time zone.",
            "Location and maps", [("lfsvc", Manual)]),
        ServiceTweak("services.maps", "Offline maps",
            "Downloads and updates offline maps. Turned off, apps cannot use or update downloaded maps.",
            "Location and maps", [("MapsBroker", Delayed)]),

        // Search and media
        ServiceTweak("services.windows-search", "Windows Search",
            "Indexes your files, email and apps in the background so searches are instant. Turned off, searching in Start and File Explorer becomes slow and less complete.",
            "Search and media", [("WSearch", Delayed)]),
        ServiceTweak("services.media-sharing", "Media Player sharing",
            "Shares your Windows Media Player library with other devices on the network. Turned off, they can no longer play it.",
            "Search and media", [("WMPNetworkSvc", Manual)]),

        // Diagnostics
        ServiceTweak("services.diagnostics-tracking", "Diagnostic data",
            "Collects and sends diagnostic data to Microsoft in the background. Turned off, nothing visible changes, but the Windows Insider Program needs it.",
            "Diagnostics", [("DiagTrack", Automatic)], recommendOff: true),
        ServiceTweak("services.error-reporting", "Error reporting",
            "Creates reports when an app crashes and sends them to Microsoft. Turned off, no crash reports are made, so Reliability Monitor shows less detail.",
            "Diagnostics", [("WerSvc", Manual)]),
        ServiceTweak("services.compatibility-assistant", "Program Compatibility Assistant",
            "Watches for older apps that run into known problems and offers fixes. Turned off, Windows no longer offers those fixes.",
            "Diagnostics", [("PcaSvc", Delayed)]),
        ServiceTweak("services.retail-demo", "Retail demo",
            "Runs the demo mode used on PCs in shops. Turned off, demo mode cannot start; nothing else changes.",
            "Diagnostics", [("RetailDemo", Manual)]),
        ServiceTweak("services.insider", "Windows Insider",
            "Runs the Windows Insider Program. Turned off, this PC cannot join it or get Insider builds.",
            "Diagnostics", [("wisvc", Manual)]),
        ServiceTweak("services.parental-controls", "Parental controls",
            "Enforces Microsoft Family limits such as screen time. Turned off, those limits stop working on this PC.",
            "Diagnostics", [("WpcMonSvc", Manual)]),
        ServiceTweak("services.spot-verifier", "Disk corruption checks",
            "Checks reported file system corruption in the background. Turned off, Windows stops checking on its own; you can still run a disk check by hand.",
            "Diagnostics", [("svsvc", Manual)]),
        ServiceTweak("services.windows-ai", "Windows AI features",
            "Runs Windows' on-device AI features. Turned off, those features may stop working.",
            "Diagnostics", [("WSAIFabricSvc", Automatic)]),

        // Diagnostics tasks
        TaskTweak("services.compatibility-appraiser", "Compatibility Appraiser",
            "Scans installed apps for upgrade compatibility and sends the results to Microsoft, which can use a lot of CPU and disk. Turned off, nothing visible changes.",
            "Diagnostics tasks", [@"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser"], recommendOff: true),
        TaskTweak("services.program-data-updater", "Program data updater",
            "Collects data about installed programs for Microsoft. Turned off, nothing visible changes.",
            "Diagnostics tasks", [@"\Microsoft\Windows\Application Experience\ProgramDataUpdater"], recommendOff: true),
        TaskTweak("services.compatibility-backup", "Compatibility data backup",
            "Backs up app compatibility data for Microsoft. Turned off, nothing visible changes.",
            "Diagnostics tasks", [@"\Microsoft\Windows\Application Experience\MareBackup"], recommendOff: true),
        TaskTweak("services.ceip-consolidator", "Customer Experience data",
            "Gathers and sends usage data for the Customer Experience Improvement Program. Turned off, nothing visible changes.",
            "Diagnostics tasks", [@"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator"], recommendOff: true),
        TaskTweak("services.usb-ceip", "USB usage data",
            "Collects data about your USB devices for Microsoft. Turned off, nothing visible changes.",
            "Diagnostics tasks", [@"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip"], recommendOff: true),
        TaskTweak("services.sqm-tasks", "Software quality data",
            "Collects software quality data for Microsoft. Turned off, nothing visible changes.",
            "Diagnostics tasks", [@"\Microsoft\Windows\PI\Sqm-Tasks"], recommendOff: true),
        TaskTweak("services.disk-diagnostic", "Disk diagnostic data",
            "Collects disk health data to send to Microsoft. Turned off, Windows no longer sends it; SMART warnings from your drive still show.",
            "Diagnostics tasks", [@"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector"], recommendOff: true),
        TaskTweak("services.autochk-proxy", "Disk check data",
            "Sends data from startup disk checks to Microsoft. Turned off, nothing visible changes.",
            "Diagnostics tasks", [@"\Microsoft\Windows\Autochk\Proxy"]),
        TaskTweak("services.feedback", "Feedback data",
            "Downloads feedback surveys and scenarios for Feedback Hub. Turned off, Windows stops asking for feedback on its own; Feedback Hub still opens.",
            "Diagnostics tasks", [@"\Microsoft\Windows\Feedback\Siuf\DmClient", @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload"]),
        TaskTweak("services.error-report-queue", "Queued error reports",
            "Sends error reports that were saved for later. Turned off, they stay on this PC.",
            "Diagnostics tasks", [@"\Microsoft\Windows\Windows Error Reporting\QueueReporting"]),
        TaskTweak("services.power-diagnostics", "Power efficiency analysis",
            "Analyzes this PC's power use on a schedule. Turned off, the analysis stops; you can still run it by hand.",
            "Diagnostics tasks", [@"\Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem"]),

        // App tasks
        TaskTweak("services.startup-app-task", "Startup app check",
            "Checks which apps start with Windows and how much they slow it down. Turned off, Windows stops telling you about new startup apps, and Task Manager's startup impact may stay empty.",
            "App tasks", [@"\Microsoft\Windows\Application Experience\StartupAppTask"]),
        TaskTweak("services.maps-update", "Offline map updates",
            "Updates downloaded maps on a schedule. Turned off, they only update when you ask.",
            "App tasks", [@"\Microsoft\Windows\Maps\MapsUpdateTask"]),
        TaskTweak("services.family-safety", "Family Safety monitor",
            "Checks Microsoft Family settings for this PC. Turned off, Family limits may not apply.",
            "App tasks", [@"\Microsoft\Windows\Shell\FamilySafetyMonitor"]),
        TaskTweak("services.recall", "Recall",
            "Sets up and runs Recall, which saves snapshots of your screen on Copilot+ PCs. Turned off, Recall stops taking snapshots.",
            "App tasks", [@"\Microsoft\Windows\WindowsAI\RecallConfiguration", @"\Microsoft\Windows\WindowsAI\RecallPipeline"]),
        TaskTweak("services.office-actions", "Office Actions Server",
            "Runs background actions for Microsoft Office. Turned off, some Office features that work in the background may stop.",
            "App tasks", [@"\Microsoft\Office\Office Actions Server"]),
    ];

    // Turning a service off only changes its start type, so a running service keeps running
    // until the next restart (as with SysMain).
    private static DeclaredTweak ServiceTweak(
        string id, string title, string description, string group,
        (string Name, ServiceStartType WindowsDefault)[] services, bool recommendOff = false)
    {
        var targets = services.Select(service => new TweakTarget(new ServiceLocation(service.Name))).ToList();
        var off = Option(targets, "off", "Off", new ServiceStartValue(ServiceStartType.Disabled));
        var on = new TweakOption("on", "On (Windows default)", targets.Zip(services)
            .ToDictionary(pair => pair.First, pair => (MachineValue?)new ServiceStartValue(pair.Second.WindowsDefault)));

        return new DeclaredTweak(id, title, description, Category.Services, group, targets, [off, on],
            Recommended: recommendOff ? off : null,
            Activation: Activation.AfterRestart);
    }

    private static DeclaredTweak TaskTweak(string id, string title, string description, string group, string[] paths, bool recommendOff = false)
    {
        var targets = paths.Select(path => new TweakTarget(new ScheduledTaskLocation(path))).ToList();
        var off = Option(targets, "off", "Off", TaskEnabledValue.Off);

        return new DeclaredTweak(id, title, description, Category.Services, group, targets, [off, Option(targets, "on", "On", TaskEnabledValue.On)],
            Recommended: recommendOff ? off : null);
    }
}
