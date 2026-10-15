using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>
/// One thing a configured install or a debloat does after the driver itself, as AkariOS does it.
/// Paths may hold environment variables (<c>%SystemDrive%</c>, <c>%ProgramData%</c>), which the
/// service expands; nothing here is a script.
/// </summary>
public abstract record DriverExtra
{
    /// <summary>The step in words, for the log and for what the Phase 1 build reports.</summary>
    public abstract string Describe();
}

/// <summary>Applies <see cref="DriverSettings.For"/>: the registry bundle, at this point in the order.</summary>
public sealed record ApplyRegistrySettings : DriverExtra
{
    public override string Describe() => "Apply the registry settings";
}

/// <summary>Downloads a tool to where AkariOS puts it.</summary>
public sealed record DownloadTool(string Name, Uri Source, string Destination) : DriverExtra
{
    public override string Describe() => $"Download {Name} from {Source} to {Destination}";
}

/// <summary>Installs an app package (.appx) for the user.</summary>
public sealed record InstallAppx(string Name, Uri Source) : DriverExtra
{
    public override string Describe() => $"Install {Name} from {Source}";
}

/// <summary>A shortcut on the desktop, or in the Start menu's common Programs folder.</summary>
/// <param name="Target">A program path, or a <c>shell:appsFolder\...</c> app id.</param>
public sealed record CreateShortcut(string Name, string Target, string WorkingDirectory, bool StartMenu = false) : DriverExtra
{
    public override string Describe() => $"Put a \"{Name}\" shortcut {(StartMenu ? "in the Start menu" : "on the desktop")}";
}

/// <summary>Moves a file into a folder, replacing one of the same name; nothing when it is not there.</summary>
public sealed record MoveFile(string From, string ToFolder) : DriverExtra
{
    public override string Describe() => $"Move {From} to {ToFolder}";
}

/// <summary>Deletes a file or a whole folder; nothing when it is not there.</summary>
public sealed record RemovePath(string Path) : DriverExtra
{
    public override string Describe() => $"Remove {Path}";
}

/// <summary>Stops a service and deletes it.</summary>
public sealed record RemoveService(string Name) : DriverExtra
{
    public override string Describe() => $"Stop and delete the {Name} service";
}

/// <summary>Deletes one registry value.</summary>
public sealed record RemoveRegistryValue(bool LocalMachine, string KeyPath, string Name) : DriverExtra
{
    public override string Describe() => $@"Delete {(LocalMachine ? "HKLM" : "HKCU")}\{KeyPath}\{Name}";
}

/// <summary>Empties a key below HKEY_CURRENT_USER: deleted, then made again with nothing in it.</summary>
public sealed record ClearUserKey(string KeyPath) : DriverExtra
{
    public override string Describe() => $@"Empty HKCU\{KeyPath}";
}

public sealed record RemoveScheduledTask(string Name) : DriverExtra
{
    public override string Describe() => $"Delete the {Name} scheduled task";
}

/// <summary>Uninstalls, quietly, the Windows Installer product whose name contains this text.</summary>
public sealed record UninstallProduct(string DisplayNameContains) : DriverExtra
{
    public override string Describe() => $"Uninstall {DisplayNameContains}";
}

public sealed record StopProcess(string Name) : DriverExtra
{
    public override string Describe() => $"Close {Name}";
}

/// <summary>Starts a program and closes it again, so it writes its first-run settings for ours to replace.</summary>
public sealed record OpenThenClose(string Program, string ProcessName, int Seconds) : DriverExtra
{
    public override string Describe() => $"Open {ProcessName} for {Seconds} seconds, then close it";
}

/// <summary>Takes the "downloaded from the internet" mark off every file in a folder.</summary>
public sealed record UnblockFiles(string Folder) : DriverExtra
{
    public override string Describe() => $"Unblock the files in {Folder}";
}

public sealed record Wait(int Seconds) : DriverExtra
{
    public override string Describe() => $"Wait {Seconds} seconds for the installer to finish in the background";
}

/// <summary>
/// Imports <see cref="DriverSettings.NvidiaProfileXml"/> with the Profile Inspector at this path,
/// or, with <paramref name="Reset"/>, the empty Base Profile AkariOS's Default imports.
/// </summary>
public sealed record ImportNvidiaProfile(string Inspector, bool Reset = false) : DriverExtra
{
    public override string Describe() => Reset
        ? "Import an empty NVIDIA Base Profile with Nvidia Profile Inspector"
        : "Import the NVIDIA profile with Nvidia Profile Inspector";
}

/// <summary>Writes one registry value; <paramref name="Vendor"/> picks the adapters for a GraphicsClass value.</summary>
public sealed record SetRegistry(GpuVendor? Vendor, DriverSetting Setting) : DriverExtra
{
    public override string Describe() => $"Set {Setting.KeyPath}\\{Setting.Name} = {Setting.Value} ({Setting.Kind})";
}

/// <summary>Deletes one registry value, or the whole key when <paramref name="Name"/> is <see langword="null"/>.</summary>
public sealed record DeleteRegistry(GpuVendor? Vendor, DriverSettingRoot Root, string KeyPath, string? Name = null) : DriverExtra
{
    public override string Describe() => Name is null ? $"Delete the key {KeyPath}" : $"Delete {KeyPath}\\{Name}";
}

/// <summary>MSISupported on every display device (Graphics.ps1:1523): 1 turns MSI mode on, 0 off.</summary>
public sealed record SetMsiMode(int Value) : DriverExtra
{
    public override string Describe() => $"Set MSISupported = {Value} on every display device";
}

/// <summary>Opens a settings page, an app or a program, as Start-Process does.</summary>
public sealed record OpenTarget(string Target) : DriverExtra
{
    public override string Describe() => $"Open {Target}";
}

/// <summary>Installs 7-Zip from the mirror when it is not there, as AkariOS does before it unpacks anything.</summary>
public sealed record EnsureSevenZip : DriverExtra
{
    public override string Describe() => "Install 7-Zip from AkariOS-Files";
}

/// <summary>Downloads a runtime installer and runs it quietly with its own arguments.</summary>
public sealed record InstallRedistributable(string Name, Uri Source, string Arguments) : DriverExtra
{
    public override string Describe() => $"Install {Name} ({Path.GetFileName(Source.LocalPath)} {Arguments})";
}

/// <summary>Downloads the DirectX runtime, unpacks it with 7-Zip and runs DXSETUP /silent (Graphics.ps1:1597-1607).</summary>
public sealed record InstallDirectX(Uri Source) : DriverExtra
{
    public override string Describe() => $"Install DirectX from {Source}";
}

/// <summary>Deletes the installer the person put in the drop folder, once it has been used.</summary>
public sealed record RemoveUsedInstaller : DriverExtra
{
    public override string Describe() => "Delete the installer from the drop folder";
}

/// <summary>
/// Everything AkariOS's driver-install-settings and driver-debloat entries do after the driver
/// (Graphics.ps1:245-765), in their order, for each vendor. The registry bundle is one step of it.
/// </summary>
public static class DriverExtras
{
    private static readonly Uri Files = new("https://github.com/isleap9/AkariOS-Files/releases/download/Files/");

    public const string NvidiaControlPanelAppId = @"shell:appsFolder\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel";

    public const string ProfileInspector = @"%ProgramFiles(x86)%\Nvidia Profile Inspector\Nvidia Profile Inspector.exe";

    private const string StartMenu = @"%ProgramData%\Microsoft\Windows\Start Menu\Programs";

    private const string RadeonSoftware = @"%SystemDrive%\Program Files\AMD\CNext\CNext\RadeonSoftware.exe";

    private const string IntelGraphicsSoftware = @"%SystemDrive%\Program Files\Intel\Intel Graphics Software\IntelGraphicsSoftware.exe";

    // "AMD Software꞉ Adrenalin Edition" - the colon is U+A789, as AMD names the folder.
    private const string AmdStartFolder = "AMD Software꞉ Adrenalin Edition";

    private const string IntelStartShortcut = "Intel® Graphics Software";

    // Graphics.ps1:270-352 and 537-560: the same in both entries.
    private static readonly IReadOnlyList<DriverExtra> Nvidia =
    [
        new InstallAppx("the NVIDIA Control Panel", new Uri(Files, "nvp.appx")),
        new CreateShortcut("NVIDIA Control Panel", NvidiaControlPanelAppId, "shell:appsFolder"),
        new RemovePath(@"%SystemDrive%\NVIDIA"),
        new ApplyRegistrySettings(),
        new UnblockFiles(@"%ProgramData%\NVIDIA Corporation\Drs"),
        new DownloadTool("Nvidia Profile Inspector", new Uri(Files, "inspector.exe"), ProfileInspector),
        new CreateShortcut("Nvidia Profile Inspector", ProfileInspector, @"%ProgramFiles(x86)%\Nvidia Profile Inspector"),
        new CreateShortcut("Nvidia Profile Inspector", ProfileInspector, @"%ProgramFiles(x86)%\Nvidia Profile Inspector", StartMenu: true),
        new ImportNvidiaProfile(ProfileInspector),
    ];

    // Graphics.ps1:367-412: after AMD's own installer.
    private static readonly IReadOnlyList<DriverExtra> AmdAfterInstall =
    [
        new CreateShortcut("AMD Radeon Software", RadeonSoftware, @"%SystemDrive%\Program Files\AMD\CNext\CNext"),
        new MoveFile($@"{StartMenu}\{AmdStartFolder}\{AmdStartFolder}.lnk", StartMenu),
        new RemovePath($@"{StartMenu}\{AmdStartFolder}"),
        new RemovePath(@"%SystemDrive%\AMD"),
        new OpenThenClose(RadeonSoftware, "RadeonSoftware", 15),
        new ApplyRegistrySettings(),
        new ClearUserKey(@"Software\AMD\CN\Notification"),
    ];

    // Graphics.ps1:610-640: what the debloat removes on top.
    private static readonly IReadOnlyList<DriverExtra> AmdDebloat =
    [
        new Wait(90),
        new RemoveRegistryValue(false, @"Software\Microsoft\Windows\CurrentVersion\Run", "AMDNoiseSuppression"),
        new RemoveRegistryValue(false, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "StartRSX"),
        new RemoveScheduledTask("StartCN"),
        new RemoveService("AMD Crash Defender Service"),
        new RemoveService("amdfendr"),
        new RemoveService("amdfendrmgr"),
        new RemoveService("amdacpbus"),
        new RemoveService("AMDSAFD"),
        new RemoveService("AtiHDAudioService"),
        new RemovePath($@"{StartMenu}\AMD Bug Report Tool"),
        new RemovePath(@"%SystemRoot%\SysWOW64\AMDBugReportTool.exe"),
        new UninstallProduct("AMD Install Manager"),
        new RemoveUsedInstaller(),
    ];

    private static readonly IReadOnlyList<DriverExtra> IntelShortcut =
    [
        new CreateShortcut("Intel Graphics Software", IntelGraphicsSoftware, @"%SystemDrive%\Program Files\Intel\Intel Graphics Software"),
    ];

    // Graphics.ps1:727-741: what the debloat removes on top.
    private static readonly IReadOnlyList<DriverExtra> IntelDebloat =
    [
        new RemoveRegistryValue(true, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", IntelStartShortcut),
        new RemoveService("IntelGFXFWupdateTool"),
        new RemoveService("cplspcon"),
        new RemoveService("CtaChildDriver"),
        new RemoveService("GSCAuxDriver"),
        new RemoveService("GSCx64"),
        new StopProcess("IntelGraphicsSoftware"),
        new StopProcess("PresentMonService"),
        new RemovePath(@"%SystemDrive%\Program Files\Intel\Intel Graphics Software\PresentMonService.exe"),
    ];

    // Graphics.ps1:437-445 and 742-746.
    private static readonly IReadOnlyList<DriverExtra> IntelAfterInstall =
    [
        new RemoveUsedInstaller(),
        new MoveFile($@"{StartMenu}\Intel\Intel Graphics Software\{IntelStartShortcut}.lnk", StartMenu),
        new RemovePath($@"{StartMenu}\Intel"),
        new RemovePath(@"%SystemDrive%\Intel"),
        new RemovePath(@"%SystemDrive%\inteldriver"),
        new ApplyRegistrySettings(),
    ];

    /// <summary>What a row does after the driver is in, in order. A plain install does nothing more.</summary>
    public static IReadOnlyList<DriverExtra> For(GpuVendor vendor, DriverStepKind kind) => (vendor, kind) switch
    {
        (_, not (DriverStepKind.InstallAndConfigure or DriverStepKind.Debloat)) => [],
        (GpuVendor.Nvidia, _) => Nvidia,
        (GpuVendor.Amd, DriverStepKind.Debloat) => [.. AmdDebloat, .. AmdAfterInstall],
        (GpuVendor.Amd, _) => AmdAfterInstall,
        (GpuVendor.Intel, DriverStepKind.Debloat) => [.. IntelShortcut, .. IntelDebloat, .. IntelAfterInstall],
        (GpuVendor.Intel, _) => [.. IntelShortcut, .. IntelAfterInstall],
        _ => [],
    };

    /// <summary>The tools a row downloads besides the driver, in a line for its confirm; empty when none.</summary>
    public static string Downloads(GpuVendor vendor, DriverStepKind kind)
    {
        var names = For(vendor, kind)
            .Select(extra => extra switch
            {
                InstallAppx appx => appx.Name,
                DownloadTool tool => tool.Name,
                _ => null,
            })
            .OfType<string>()
            .ToList();
        return names.Count == 0 ? string.Empty : string.Join(" and ", names);
    }
}
